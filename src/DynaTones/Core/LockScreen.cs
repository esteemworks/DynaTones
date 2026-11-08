using Microsoft.Win32;
using Windows.Storage;

namespace DynaTones.Core;

/// <summary>
/// The user's lock screen through Windows' own interface for it (<c>Windows.System.UserProfile.LockScreen</c>): no
/// admin rights, nothing locked, the user can pick another picture in Windows Settings at any time. The sign-in
/// screen is not changed; that needs a device policy.
/// </summary>
/// <remarks>
/// Windows refuses an image whose file name equals the one set before, so every image is set from a copy with a time
/// in its name, in the data folder; only the copy on the lock screen now is kept. Which source a copy came from is
/// written next to it, so a later start can tell "our image, still right" from "someone chose another".
/// </remarks>
public static class LockScreen
{
    static string Folder => Path.Combine(Paths.Data, "lockscreen");
    static string Record => Path.Combine(Folder, "applied.txt");

    const string SpotlightKey = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    /// <summary>The file the lock screen shows now, if it was set from a file; null for Spotlight or the Windows default.</summary>
    public static string? Current()
    {
        try
        {
            return Windows.System.UserProfile.LockScreen.OriginalImageFile is { IsFile: true } uri ? uri.LocalPath : null;
        }
        catch (Exception ex)
        {
            Log.Debug($"Reading the lock screen failed: {ex.Message}", "LockScreen");
            return null;
        }
    }

    /// <summary>The source image of the copy DynaTones put on the lock screen last, if that copy is still there.</summary>
    public static string? AppliedSource()
    {
        if (!File.Exists(Record)) return null;
        var lines = File.ReadAllLines(Record);
        return lines.Length == 2 && Current() is { } shown && Wallpaper.SamePath(shown, lines[1]) ? lines[0] : null;
    }

    /// <summary>True when the lock screen shows <paramref name="source"/> through DynaTones' copy.</summary>
    public static bool Shows(string source) => AppliedSource() is { } applied && Wallpaper.SamePath(applied, source);

    /// <summary>Puts <paramref name="source"/> on the lock screen. Runs off the window's thread; returns an error text or null.</summary>
    public static async Task<string?> SetAsync(string source)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var copy = Path.Combine(Folder, $"{Path.GetFileNameWithoutExtension(source)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(source)}");
            File.Copy(source, copy, overwrite: true);
            var file = await StorageFile.GetFileFromPathAsync(copy);
            await Windows.System.UserProfile.LockScreen.SetImageFileAsync(file);
            if (Current() is not { } shown || !Wallpaper.SamePath(shown, copy))
                return Strings.T("Windows did not take the lock screen picture.", "Windows hat das Sperrbildschirmbild nicht übernommen.");
            File.WriteAllLines(Record, [source, copy]);
            // Only the copy on the lock screen now is needed.
            foreach (var old in Directory.GetFiles(Folder).Where(f => !Wallpaper.SamePath(f, copy) && !Wallpaper.SamePath(f, Record)))
            {
                try { File.Delete(old); } catch (IOException) { }
            }
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning($"Setting the lock screen failed: {ex.Message}", "LockScreen");
            return Strings.T($"Windows did not take the lock screen picture: {ex.Message}",
                $"Windows hat das Sperrbildschirmbild nicht übernommen: {ex.Message}");
        }
    }

    /// <summary>Why the lock screen cannot be changed on this device, or null.</summary>
    public static string? Blocked()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Personalization"))
        {
            if (key?.GetValue("NoChangingLockScreen") is int no && no == 1)
                return Strings.T("Your organization does not allow changing the lock screen.", "Deine Organisation erlaubt nicht, den Sperrbildschirm zu ändern.");
            if (key?.GetValue("LockScreenImage") is string fixedImage && fixedImage.Length > 0)
                return Strings.T("Your organization sets the lock screen picture.", "Deine Organisation legt das Sperrbildschirmbild fest.");
        }
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP"))
        {
            // DynaTones' own value for the screen before sign-in is no rule of the organization (07.11.2026: switching
            // "before signing in" on made DynaTones report its own picture as one).
            if (key?.GetValue("LockScreenImagePath") is string path && path.Length > 0 && !SignIn.IsOurs(path))
                return Strings.T("Your organization sets the lock screen picture.", "Deine Organisation legt das Sperrbildschirmbild fest.");
        }
        return null;
    }

    /// <summary>Whether Windows spotlight shows its own pictures on the lock screen.</summary>
    public static bool SpotlightOn
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(SpotlightKey);
            return key?.GetValue("RotatingLockScreenEnabled") is int on && on == 1;
        }
    }

    /// <summary>The value Spotlight had, so it can be given back; null when there was none.</summary>
    public static int? SpotlightValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SpotlightKey);
        return key?.GetValue("RotatingLockScreenEnabled") as int?;
    }

    /// <summary>
    /// Switches Windows spotlight on the lock screen off, or gives back the value it had. These per-user values are the
    /// ones Windows Settings writes; they are not documented, so DynaTones changes them only when the user asks.
    /// </summary>
    public static void SetSpotlight(int? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SpotlightKey);
        if (value is { } v)
        {
            key.SetValue("RotatingLockScreenEnabled", v, RegistryValueKind.DWord);
            key.SetValue("RotatingLockScreenOverlayEnabled", v, RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue("RotatingLockScreenEnabled", throwOnMissingValue: false);
            key.DeleteValue("RotatingLockScreenOverlayEnabled", throwOnMissingValue: false);
        }
    }
}
