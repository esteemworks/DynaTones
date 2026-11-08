namespace DynaTones.Core;

/// <summary>
/// What a profile changes in DynaTones. Every value is optional: what a profile leaves out is taken over from the
/// settings under Background and Color mode ("übernommen"). The profiles themselves (name, color, schedule, which
/// apps use them) live in the family's shared file (EsteemWorks.Kit FamilyProfiles), so StartQueue and DynaTones can
/// use the same one.
/// </summary>
public sealed class ProfileSettings
{
    public string? DesktopPackage { get; set; }
    /// <summary>A transition as <see cref="Transitions.Code"/>.</summary>
    public int? Transition { get; set; }
    public ProfileLock? Lock { get; set; }
    public bool? SignIn { get; set; }
    /// <summary>The color mode of apps and of Windows together.</summary>
    public ModeTarget? ColorMode { get; set; }
    public ProfilePointer? Pointer { get; set; }

    public bool IsEmpty => DesktopPackage == null && Transition == null && Lock == null && SignIn == null && ColorMode == null && Pointer == null;
}

/// <summary>The lock screen in a profile: like Windows (off), like the desktop, or a package of its own.</summary>
public sealed class ProfileLock
{
    public bool Enabled { get; set; } = true;
    public LockScreenSource Source { get; set; }
    public string? PackageId { get; set; }
}

public sealed class ProfilePointer
{
    public string? Light { get; set; }
    public string? Dark { get; set; }
}

public static class ProfileOverrides
{
    public const string App = "DynaTones";

    /// <summary>The settings as they apply with a profile: a copy of the settings with the profile's values set.</summary>
    public static Config Apply(Config settings, Guid? profile)
    {
        if (profile is not { } id || !settings.Profiles.TryGetValue(id, out var p) || p.IsEmpty) return settings;
        var c = System.Text.Json.JsonSerializer.Deserialize<Config>(System.Text.Json.JsonSerializer.Serialize(settings, Json.Options), Json.Options)!;
        if (p.DesktopPackage != null)
        {
            c.Wallpaper.PackageId = p.DesktopPackage;
            c.Wallpaper.Enabled = true;
        }
        if (p.Transition is { } code)
        {
            var t = Transitions.FromCode(code);
            c.Wallpaper.Fade = t.Fade;
            c.Wallpaper.BlendMinutes = t.BlendMinutes;
            c.Wallpaper.BlendSeconds = t.BlendSeconds;
        }
        if (p.Lock is { } l)
        {
            c.LockScreen.Enabled = l.Enabled;
            c.LockScreen.Source = l.Source;
            if (l.PackageId != null) c.LockScreen.PackageId = l.PackageId;
        }
        if (p.SignIn is { } signIn) c.LockScreen.SignIn = signIn;
        if (p.ColorMode is { } mode)
        {
            c.Appearance.Apps = mode;
            c.Appearance.System = mode;
        }
        if (p.Pointer is { } pointer)
        {
            c.Appearance.CursorLight = pointer.Light;
            c.Appearance.CursorDark = pointer.Dark;
        }
        return c;
    }
}
