using Microsoft.Win32;

namespace DynaTones.Core;

/// <summary>
/// Windows' "accent color from the background" (Settings, Personalization, Colors, Automatic) works out a new accent
/// for every new background and tells every window, two to six times. Explorer windows in dark mode then draw their
/// scroll bars anew, and they flash white. A flowing transition sets a picture in between every minute, so Explorer
/// flickered every minute (found 12.11.2026 on the author's PC: a burst of ImmersiveColorSet 1.5 s after each step).
/// While DynaTones shows pictures in between (the steps of a flowing or gentle transition, the frames of a fade) it
/// switches that automatic off, and on again right before the target picture, so Windows works out the accent once,
/// from the picture it is meant for. Only when it was on. That it was on is kept in a file in DynaTones' data folder,
/// not in the settings: an exported settings file on another PC must not switch it on there. It goes back on in any
/// case: with the target picture, on pausing, when the background is off, on quitting, and at the next start after
/// DynaTones ended in the middle. Both changes are a line in the log. The value is read by Windows at the next
/// background; setting it sends no message to the windows (checked in Windows Sandbox).
/// </summary>
public static class AccentHold
{
    const string DesktopKey = @"Control Panel\Desktop";
    const string ValueName = "AutoColorization";

    static string Marker => Path.Combine(Paths.Data, "accent-held");

    /// <summary>DynaTones switched the automatic accent off and owes switching it on.</summary>
    public static bool Held => File.Exists(Marker);

    static bool? Automatic
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
            return key?.GetValue(ValueName) is int value ? value == 1 : null;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(DesktopKey);
            key.SetValue(ValueName, value == true ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    /// <summary>Before a picture in between: the automatic accent off, when it is on and not held yet.</summary>
    public static void Hold(string reason)
    {
        if (DayEngine.DryRun || Held || Automatic != true) return;
        try
        {
            Directory.CreateDirectory(Paths.Data);
            File.WriteAllText(Marker, DateTime.Now.ToString("s"));
            Automatic = false;
            Log.Info($"Accent color from the background paused while pictures in between show ({reason})");
        }
        catch (Exception ex)
        {
            Log.Warning($"The accent color from the background could not be paused: {ex.Message}");
        }
    }

    /// <summary>Before the target picture, and whenever the pictures in between end: the automatic accent on again.</summary>
    public static void Release(string reason)
    {
        if (!Held) return;
        try
        {
            Automatic = true;
            File.Delete(Marker);
            Log.Info($"Accent color from the background on again ({reason})");
        }
        catch (Exception ex)
        {
            Log.Warning($"The accent color from the background could not be switched on again: {ex.Message}");
        }
    }
}
