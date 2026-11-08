using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DynaTones.Core;

/// <summary>
/// Sets and reads the desktop background through the Windows desktop interface (IDesktopWallpaper), per monitor.
/// Not the Store interface: that one resets how the image fits. Everything here runs as the user.
/// </summary>
public static class Wallpaper
{
    /// <summary>The image of every active monitor, by monitor id.</summary>
    public static Dictionary<string, string> Current()
    {
        var result = new Dictionary<string, string>();
        var desktop = Create();
        try
        {
            foreach (var monitor in Monitors(desktop))
            {
                try
                {
                    result[monitor] = desktop.GetWallpaper(monitor) ?? "";
                }
                catch (COMException)
                {
                    result[monitor] = "";
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(desktop);
        }
        return result;
    }

    /// <summary>
    /// Puts the image on every monitor that does not show it yet. Right after waking up Windows sometimes is not
    /// ready yet (the class is not registered for a moment), so it tries up to three times.
    /// </summary>
    public static bool Set(string path, WallpaperFit fit, bool fade)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (fade) PrepareFade();
                var desktop = Create();
                try
                {
                    var position = (int)ToPosition(fit);
                    if (desktop.GetPosition() != position) desktop.SetPosition(position);
                    // One call for all monitors: Windows then draws the change once, as one fade. Set monitor by
                    // monitor, each call redrew the whole desktop, and with three monitors every step of a flowing
                    // transition flashed three times (seen 07.11.2026).
                    desktop.SetWallpaper(null, path);
                }
                finally
                {
                    Marshal.ReleaseComObject(desktop);
                }
                return true;
            }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(1000);
            }
        }
        Log.Error("Setting the wallpaper failed", last, eventId: Events.WallpaperFailed);
        return false;
    }

    /// <summary>
    /// The pixels an image has to cover: across all monitors when it spans them, else the widest and the tallest
    /// monitor, so one size suits each. Null when Windows does not say.
    /// </summary>
    public static (int Width, int Height)? Coverage(WallpaperFit fit)
    {
        try
        {
            var desktop = Create();
            try
            {
                var rects = Monitors(desktop).Select(m => desktop.GetMonitorRECT(m)).ToList();
                if (rects.Count == 0) return null;
                if (fit == WallpaperFit.Span)
                    return (rects.Max(r => r.Right) - rects.Min(r => r.Left), rects.Max(r => r.Bottom) - rects.Min(r => r.Top));
                return (rects.Max(r => r.Right - r.Left), rects.Max(r => r.Bottom - r.Top));
            }
            finally
            {
                Marshal.ReleaseComObject(desktop);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool SamePath(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Why the background cannot be changed here, or null. A policy of the organization wins; DynaTones says so and
    /// keeps the rest running.
    /// </summary>
    public static string? Blocked()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"))
            if (key?.GetValue("NoChangingWallPaper") is int no && no != 0)
                return Strings.T("Your organization does not allow changing the desktop background.",
                    "Deine Organisation erlaubt nicht, den Desktophintergrund zu ändern.");
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
            if (key?.GetValue("Wallpaper") is string { Length: > 0 })
                return Strings.T("Your organization sets the desktop background.", "Deine Organisation gibt den Desktophintergrund vor.");
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP"))
            if (key?.GetValue("DesktopImagePath") is string { Length: > 0 })
                return Strings.T("Your organization sets the desktop background through device management.",
                    "Deine Organisation gibt den Desktophintergrund über die Geräteverwaltung vor.");
        return null;
    }

    /// <summary>Other things that change the background: the Windows slideshow or Spotlight.</summary>
    public static string? Competitor()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
        return key?.GetValue("BackgroundType") switch
        {
            2 => Strings.T("The Windows slideshow is on for the background.", "Für den Hintergrund ist die Windows-Diashow an."),
            3 => Strings.T("Windows Spotlight is on for the background.", "Für den Hintergrund ist Windows-Blickpunkt an."),
            _ => null,
        };
    }

    static IEnumerable<string> Monitors(IDesktopWallpaper desktop)
    {
        var count = desktop.GetMonitorDevicePathCount();
        for (uint i = 0; i < count; i++)
        {
            string id;
            try
            {
                id = desktop.GetMonitorDevicePathAt(i);
                // Known but not connected: has no rectangle.
                desktop.GetMonitorRECT(id);
            }
            catch (COMException)
            {
                continue;
            }
            yield return id;
        }
    }

    static Position ToPosition(WallpaperFit fit) => fit switch
    {
        WallpaperFit.Fit => Position.Fit,
        WallpaperFit.Stretch => Position.Stretch,
        WallpaperFit.Center => Position.Center,
        WallpaperFit.Span => Position.Span,
        _ => Position.Fill,
    };

    /// <summary>
    /// Asks Explorer to set up its layer behind the desktop icons, which Windows needs to fade from one background to
    /// the next. Undocumented but long-standing; without it the image simply switches.
    /// </summary>
    static void PrepareFade()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero) SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
    }

    static IDesktopWallpaper Create() => (IDesktopWallpaper)new DesktopWallpaperClass();

    enum Position { Center = 0, Tile = 1, Stretch = 2, Fit = 3, Fill = 4, Span = 5 }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        Rect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
        void SetSlideshow(IntPtr items);
        IntPtr GetSlideshow();
        void SetSlideshowOptions(int options, uint slideshowTick);
        void GetSlideshowOptions(out int options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, int direction);
        int GetStatus();
        void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
    }

    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    class DesktopWallpaperClass;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
