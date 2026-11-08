using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DynaTones.Core;

/// <summary>
/// Light and dark for apps and for Windows itself (taskbar, Start), and the mouse pointer scheme. All of it are
/// settings of the user: no admin rights.
/// </summary>
/// <remarks>
/// Windows has no public interface for switching the mode the way Settings does. Writing the two values and telling
/// every window works for apps and, on current Windows 11, for the taskbar; Auto Dark Mode found the taskbar can be
/// left half switched with several monitors, a Windows problem. A theme-file route can follow if that shows up here.
/// </remarks>
public static class Appearance
{
    const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool? AppsLight => ReadLight("AppsUseLightTheme");
    public static bool? SystemLight => ReadLight("SystemUsesLightTheme");

    static bool? ReadLight(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(name) is int v ? v != 0 : null;
    }

    /// <summary>Sets what differs and tells the windows once. True when anything changed.</summary>
    public static bool Apply(bool? appsLight, bool? systemLight)
    {
        var changed = false;
        using (var key = Registry.CurrentUser.CreateSubKey(PersonalizeKey))
        {
            if (appsLight is { } apps && AppsLight != apps)
            {
                key.SetValue("AppsUseLightTheme", apps ? 1 : 0, RegistryValueKind.DWord);
                changed = true;
            }
            if (systemLight is { } system && SystemLight != system)
            {
                key.SetValue("SystemUsesLightTheme", system ? 1 : 0, RegistryValueKind.DWord);
                changed = true;
            }
        }
        // The values are in place at once; telling every window can take seconds when one of them is slow, so that
        // runs beside the engine instead of holding up the background at sign-in.
        if (changed) Task.Run(Broadcast);
        return changed;
    }

    static void Broadcast()
    {
        var text = Marshal.StringToHGlobalUni("ImmersiveColorSet");
        try
        {
            SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, text, SmtoAbortIfHung, 2000, out _);
        }
        finally
        {
            Marshal.FreeHGlobal(text);
        }
        SendMessageTimeout(HwndBroadcast, WmThemeChanged, IntPtr.Zero, IntPtr.Zero, SmtoAbortIfHung, 2000, out _);
    }

    // ---------- Mouse pointer schemes ----------

    static readonly string[] Roles =
    [
        "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
        "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person",
    ];

    /// <summary>The pointer schemes of the user and of Windows, by name.</summary>
    public static IReadOnlyList<string> CursorSchemes() => Schemes().Keys.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

    public static string? CurrentCursorScheme
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
            return key?.GetValue("") as string;
        }
    }

    static Dictionary<string, (string Files, int Source)> Schemes()
    {
        var result = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase);
        using (var system = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes"))
            if (system != null)
                foreach (var name in system.GetValueNames())
                    if (system.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string files) result[name] = (files, 2);
        using (var user = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors\Schemes"))
            if (user != null)
                foreach (var name in user.GetValueNames())
                    if (user.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string files) result[name] = (files, 1);
        return result;
    }

    /// <summary>Switches to a pointer scheme by name. False when it does not exist.</summary>
    public static bool ApplyCursorScheme(string name)
    {
        if (string.Equals(CurrentCursorScheme, name, StringComparison.OrdinalIgnoreCase)) return false;
        if (!Schemes().TryGetValue(name, out var scheme)) return false;
        var files = scheme.Files.Split(',');
        using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
        {
            for (var i = 0; i < Roles.Length; i++)
                key.SetValue(Roles[i], i < files.Length ? files[i] : "", RegistryValueKind.ExpandString);
            key.SetValue("", name);
            key.SetValue("Scheme Source", scheme.Source, RegistryValueKind.DWord);
        }
        SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, SpifUpdateIniFile | SpifSendChange);
        return true;
    }

    static readonly IntPtr HwndBroadcast = new(0xFFFF);
    const uint WmSettingChange = 0x001A;
    const uint WmThemeChanged = 0x031A;
    const uint SmtoAbortIfHung = 0x0002;
    const uint SpiSetCursors = 0x0057;
    const uint SpifUpdateIniFile = 0x01;
    const uint SpifSendChange = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint winIni);
}
