using System.Text.Json.Serialization;

namespace DynaTones.Core;

// ---------- Settings ----------

public sealed class Config
{
    /// <summary>Today's shape of config.json. Raise it only with a migration in App.xaml.cs (KitSettings.Configure).</summary>
    public const int Schema = 1;

    public int Version { get; set; } = Schema;
    public string? Language { get; set; }
    public LocationSettings Location { get; set; } = new();
    public WallpaperSettings Wallpaper { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public LockScreenSettings LockScreen { get; set; } = new();
    /// <summary>Nothing changes until then; null when not paused.</summary>
    public DateTime? PausedUntil { get; set; }
    public ThemeChoice WindowTheme { get; set; } = ThemeChoice.System;
    /// <summary>What each profile changes, by the profile's id in the family's shared file; see <see cref="ProfileSettings"/>.</summary>
    public Dictionary<Guid, ProfileSettings> Profiles { get; set; } = [];
    /// <summary>The organization's recommendations as last applied, per policy, so each is applied once (see Policies).</summary>
    public Dictionary<string, string> PolicyApplied { get; set; } = [];
}

public enum LocationMode
{
    /// <summary>The first that works: Windows location, the place or coordinates entered, the time zone.</summary>
    Auto,
    Windows,
    Coordinates,
    TimeZone,
    /// <summary>No sun at all: only own times are used.</summary>
    None,
}

public sealed class LocationSettings
{
    public LocationMode Mode { get; set; } = LocationMode.Auto;
    /// <summary>Entered by hand, or a place picked from the list.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PlaceName { get; set; }
    /// <summary>The last position Windows reported, so a start without location still knows where it is.</summary>
    public GeoPoint? LastWindows { get; set; }
}

public sealed record GeoPoint(double Latitude, double Longitude, DateTime At);

/// <summary>Where the lock screen takes its images from.</summary>
public enum LockScreenSource
{
    /// <summary>The image the desktop shows, at the same moments.</summary>
    Wallpaper,
    /// <summary>A package of its own, placed through the day like the wallpaper's.</summary>
    Package,
}

public sealed class LockScreenSettings
{
    /// <summary>Off until the user switches it on: the lock screen is a picture people often chose themselves.</summary>
    public bool Enabled { get; set; }
    public LockScreenSource Source { get; set; } = LockScreenSource.Wallpaper;
    public string? PackageId { get; set; }
    /// <summary>
    /// Windows spotlight replaces the lock screen with its own pictures. Switched off by DynaTones only after the user
    /// agreed; <see cref="SpotlightBefore"/> keeps what it was, to give it back.
    /// </summary>
    public bool TurnOffSpotlight { get; set; }
    public int? SpotlightBefore { get; set; }
    /// <summary>
    /// Also the screen before anyone signs in, through the device (needs the installation with admin rights, see
    /// <see cref="SignIn"/>). Off until switched on: it changes what every user of the PC sees before signing in.
    /// </summary>
    public bool SignIn { get; set; }
}

public enum WallpaperFit { Fill, Fit, Stretch, Center, Span }

public sealed class WallpaperSettings
{
    public bool Enabled { get; set; } = true;
    public string? PackageId { get; set; }
    public WallpaperFit Fit { get; set; } = WallpaperFit.Fill;
    /// <summary>Let Windows fade from one image to the next.</summary>
    public bool Fade { get; set; } = true;
    /// <summary>
    /// Minutes before the next image in which the two are mixed in small steps, so light changes flow instead of
    /// jumping; 0 for a plain change. Capped by how long the current image is shown. Only with <see cref="Fade"/>.
    /// </summary>
    public int BlendMinutes { get; set; } = 30;
    /// <summary>
    /// "Gentle": seconds before the next image in which three mixed steps show, each faded in by Windows; 0 for none.
    /// Only with <see cref="Fade"/> and <see cref="BlendMinutes"/> 0.
    /// </summary>
    public int BlendSeconds { get; set; }
    /// <summary>
    /// "Next image": the natural start of the image shown ahead of its time. Until then the plan is one step ahead,
    /// the flowing transition included; from then on it is the plan anyway. Null: no skip.
    /// </summary>
    public DateTime? SkipTo { get; set; }
}

public enum AppearanceTrigger
{
    /// <summary>Light from sunrise to sunset, each with an offset.</summary>
    Sun,
    /// <summary>Light and dark from fixed times.</summary>
    Times,
    /// <summary>Dark while the image shown belongs below the horizon.</summary>
    Image,
}

public enum ModeTarget { Switch, AlwaysLight, AlwaysDark, Leave }

public sealed class AppearanceSettings
{
    public bool Enabled { get; set; } = true;
    public AppearanceTrigger Trigger { get; set; } = AppearanceTrigger.Sun;
    public int SunriseOffsetMinutes { get; set; } = 30;
    public int SunsetOffsetMinutes { get; set; } = -45;
    public string LightFrom { get; set; } = "07:00";
    public string DarkFrom { get; set; } = "19:00";
    public ModeTarget Apps { get; set; } = ModeTarget.Switch;
    public ModeTarget System { get; set; } = ModeTarget.Switch;
    /// <summary>Name of a pointer scheme for light and for dark; null leaves the pointer alone.</summary>
    public string? CursorLight { get; set; }
    public string? CursorDark { get; set; }
    /// <summary>
    /// "Switch to dark" or "light" by hand: the other mode now, until the plan comes to it by itself; from then on the
    /// day goes on as planned. Null: as planned.
    /// </summary>
    public ModeHold? Hold { get; set; }
}

/// <summary>
/// The color mode set by hand: <see cref="Dark"/> while the plan says otherwise, until its next change of mode at
/// <see cref="Until"/>. 20 minutes more allowed, as that moment moves a little when the position is refined.
/// </summary>
public sealed class ModeHold
{
    public bool Dark { get; set; }
    public DateTime Until { get; set; }
}

// ---------- Image packages ----------

public enum AnchorMode
{
    /// <summary>DynaTones decides, from the package's data, the photo or the order of the images.</summary>
    Auto,
    /// <summary>From when the sun reaches an elevation on its way up or down.</summary>
    Sun,
    /// <summary>From a fixed time.</summary>
    Time,
    /// <summary>Not shown.</summary>
    Off,
}

/// <summary>
/// Where in the day an image starts. It is shown from here until the next image starts. Elevations are on the
/// reference arc (<see cref="SunDay.Arc"/>): between -12° and +10° they are the real ones.
/// </summary>
public sealed class Anchor
{
    public AnchorMode Mode { get; set; } = AnchorMode.Auto;
    public double Elevation { get; set; }
    public bool Rising { get; set; } = true;
    /// <summary>"HH:mm"; only for <see cref="AnchorMode.Time"/>.</summary>
    public string? Time { get; set; }

    public Anchor Clone() => (Anchor)MemberwiseClone();
}

/// <summary>What DynaTones proposed for an image, kept so "Auto" can always go back to it.</summary>
public sealed class Suggestion
{
    public double Elevation { get; set; }
    public bool Rising { get; set; }
    public ImageKind Kind { get; set; }
    /// <summary>Why, in English; <see cref="ReasonDe"/> in German. Packages imported before 06.11.2026 have only this one, in the language of their import.</summary>
    public string Reason { get; set; } = "";
    public string? ReasonDe { get; set; }
    /// <summary>The reason in the language shown now.</summary>
    [JsonIgnore] public string ReasonShown => Strings.German && !string.IsNullOrEmpty(ReasonDe) ? ReasonDe : Reason;
    /// <summary>0 to 1: how sure the suggestion is.</summary>
    public double Confidence { get; set; }
}

public enum ImageKind { Night, Dawn, Day, Dusk }

public sealed class PackageImage
{
    public string File { get; set; } = "";
    /// <summary>A name to show instead of the file name, such as "Blue hour", when the package brings one.</summary>
    public string? Title { get; set; }
    public string? TitleDe { get; set; }

    /// <summary>
    /// The name shown: the package's own title, a name for the file names of DynaTones' gallery packages made before
    /// they had titles, or the file name without its extension.
    /// </summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (Strings.German && !string.IsNullOrEmpty(TitleDe)) return TitleDe;
            if (!string.IsNullOrEmpty(Title)) return Title;
            var stem = System.IO.Path.GetFileNameWithoutExtension(File);
            return KnownNames.TryGetValue(stem, out var name) ? Strings.T(name.En, name.De) : stem;
        }
    }

    static readonly Dictionary<string, (string En, string De)> KnownNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1-night"] = ("Night", "Nacht"),
        ["2-blue-hour-morning"] = ("Blue hour", "Blaue Stunde"),
        ["2-blue-hour"] = ("Blue hour", "Blaue Stunde"),
        ["3-sunrise"] = ("Sunrise", "Sonnenaufgang"),
        ["4-morning"] = ("Morning", "Morgen"),
        ["5-noon"] = ("Noon", "Mittag"),
        ["6-afternoon"] = ("Afternoon", "Nachmittag"),
        ["7-golden-hour"] = ("Golden hour", "Goldene Stunde"),
        ["7-sunset"] = ("Sunset", "Sonnenuntergang"),
        ["8-sunset"] = ("Sunset", "Sonnenuntergang"),
        ["8-dusk"] = ("Dusk", "Dämmerung"),
        ["9-blue-hour-evening"] = ("Evening blue hour", "Abendliche blaue Stunde"),
    };
    public Anchor Anchor { get; set; } = new();
    public Suggestion? Suggested { get; set; }
    /// <summary>Measured on import, for the suggestion and the light/dark trigger.</summary>
    public ImageLook? Look { get; set; }
}

public sealed class ImageLook
{
    /// <summary>Mean brightness, 0 to 1.</summary>
    public double Brightness { get; set; }
    /// <summary>Brightness of the upper third, usually the sky.</summary>
    public double SkyBrightness { get; set; }
    /// <summary>Red over blue in the sky: above 0 warm (sunrise, sunset), below 0 cool (day, blue hour).</summary>
    public double Warmth { get; set; }
    public double Saturation { get; set; }
    /// <summary>The time the photo was taken, from its data, if any.</summary>
    public DateTime? TakenAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>A package's own transition, instead of the one set for the desktop.</summary>
public sealed class TransitionSetting
{
    public bool Fade { get; set; } = true;
    public int BlendMinutes { get; set; }
    public int BlendSeconds { get; set; }
}

/// <summary>
/// The four transitions as one number, as the pages offer them: -1 at once, 0 soft (Windows fades), -2 gentle (a few
/// seconds in three steps), above 0 flowing over that many minutes.
/// </summary>
public static class Transitions
{
    public const int Gentle = -2, GentleSeconds = 6;

    public static (bool Fade, int Minutes, int Seconds) Effective(WallpaperSettings w, Package? p) =>
        p?.Transition is { } t ? (t.Fade, t.BlendMinutes, t.BlendSeconds) : (w.Fade, w.BlendMinutes, w.BlendSeconds);

    public static int Code(bool fade, int minutes, int seconds) => !fade ? -1 : minutes > 0 ? minutes : seconds > 0 ? Gentle : 0;

    public static TransitionSetting FromCode(int code) => new()
    {
        Fade = code != -1,
        BlendMinutes = Math.Max(0, code),
        BlendSeconds = code == Gentle ? GentleSeconds : 0,
    };
}

public sealed class Package
{
    public string Name { get; set; } = "";
    /// <summary>The German name, when the package brings one (packages from the gallery and the setup do).</summary>
    public string? NameDe { get; set; }
    public string? Description { get; set; }
    public string? DescriptionDe { get; set; }
    public string? Credits { get; set; }
    public string? CreditsDe { get; set; }
    /// <summary>The pictures were made with an AI model; said wherever the package is shown.</summary>
    public bool AiMade { get; set; }
    /// <summary>The id in the gallery (see <see cref="Gallery"/>) for packages that came from it or from the setup.</summary>
    public string? GalleryId { get; set; }
    /// <summary>Its own transition; null: the one set for the desktop.</summary>
    public TransitionSetting? Transition { get; set; }
    public List<PackageImage> Images { get; set; } = [];
    /// <summary>Use the evening images in the morning too when a package has none for the morning.</summary>
    public bool MirrorEvening { get; set; } = true;
    /// <summary>Where it came from: folder, zip, ddw.</summary>
    public string? Source { get; set; }

    [JsonIgnore] public string Id { get; set; } = "";
    [JsonIgnore] public string Folder { get; set; } = "";

    /// <summary>The name in the language shown now.</summary>
    [JsonIgnore] public string DisplayName => Strings.German && !string.IsNullOrEmpty(NameDe) ? NameDe : Name;
    [JsonIgnore] public string? DescriptionShown => Strings.German && !string.IsNullOrEmpty(DescriptionDe) ? DescriptionDe : Description;
    [JsonIgnore] public string? CreditsShown => Strings.German && !string.IsNullOrEmpty(CreditsDe) ? CreditsDe : Credits;

    public string PathOf(PackageImage image) => System.IO.Path.Combine(Folder, image.File);
}
