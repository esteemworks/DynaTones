using System.Globalization;

namespace DynaTones.Core;

/// <summary>
/// What an organization can steer in DynaTones, through Group Policy, Intune or any tool that writes the registry
/// (see KitPolicy in EsteemWorks.Kit). This list is the one source: DynaTones reads the policies from it and
/// <c>--write-admx</c> writes the ADMX and ADML templates from it. Fixed values are laid over the user's settings
/// when they are read and never written into them, so the user's own choice is back when a policy goes away.
/// </summary>
public static class Policies
{
    public const string WallpaperEnabled = nameof(WallpaperEnabled);
    public const string ManagedPackageFolder = nameof(ManagedPackageFolder);
    public const string AllowUserImport = nameof(AllowUserImport);
    public const string PackageGallery = nameof(PackageGallery);
    public const string ColorModeEnabled = nameof(ColorModeEnabled);
    public const string AppsColorMode = nameof(AppsColorMode);
    public const string WindowsColorMode = nameof(WindowsColorMode);
    public const string LocationMode = nameof(LocationMode);
    public const string Coordinates = nameof(Coordinates);
    public const string Language = nameof(Language);
    public const string StartWithWindows = nameof(StartWithWindows);
    public const string LockScreenEnabled = nameof(LockScreenEnabled);
    public const string SignInScreen = nameof(SignInScreen);

    static readonly PolicyChoice[] ModeChoices =
    [
        new("switch", "Switch with the day", "Mit dem Tag wechseln"),
        new("light", "Always light", "Immer hell"),
        new("dark", "Always dark", "Immer dunkel"),
        new("leave", "Leave as it is", "Nicht ändern"),
    ];

    public static void Define() => KitPolicy.Define(
        new Policy(WallpaperEnabled, PolicyType.Toggle, "Change the wallpaper", "Hintergrund wechseln",
            "Whether DynaTones changes the desktop background through the day.",
            "Ob DynaTones den Desktophintergrund durch den Tag wechselt."),
        new Policy(ManagedPackageFolder, PolicyType.Text, "Image package from a managed folder", "Bildpaket aus einem verwalteten Ordner",
            "A folder with the organization's images, a .zip or a WinDynamicDesktop .ddw, for example under %ProgramData%. DynaTones imports it when it changes and shows it. Environment variables are expanded.",
            "Ein Ordner mit den Bildern der Organisation, eine .zip oder eine WinDynamicDesktop-.ddw, zum Beispiel unter %ProgramData%. DynaTones importiert ihn, wenn er sich ändert, und zeigt ihn. Umgebungsvariablen werden aufgelöst."),
        new Policy(AllowUserImport, PolicyType.Toggle, "Users may add image packages", "Nutzer dürfen Bildpakete hinzufügen",
            "Whether users can add their own images and packages.",
            "Ob Nutzer eigene Bilder und Pakete hinzufügen können."),
        new Policy(PackageGallery, PolicyType.Text, "Where the gallery takes packages from", "Woher die Galerie Pakete nimmt",
            "A folder, a share or a web address with a packages.json and the packages next to it, for the organization's own gallery. 'off' hides the gallery. Not set: the packages of the DynaTones releases on GitHub. Environment variables are expanded. Without the policy 'Users may add image packages' there is no gallery either.",
            "Ein Ordner, eine Freigabe oder eine Webadresse mit einer packages.json und den Paketen daneben, für eine eigene Galerie der Organisation. 'off' blendet die Galerie aus. Nicht gesetzt: die Pakete aus den DynaTones-Versionen auf GitHub. Umgebungsvariablen werden aufgelöst. Ohne die Richtlinie 'Nutzer dürfen Bildpakete hinzufügen' gibt es auch keine Galerie."),
        new Policy(ColorModeEnabled, PolicyType.Toggle, "Switch the color mode", "Farbmodus wechseln",
            "Whether DynaTones switches Windows and apps between light and dark.",
            "Ob DynaTones Windows und Apps zwischen hell und dunkel umschaltet."),
        new Policy(AppsColorMode, PolicyType.Choice, "Color mode of apps", "Farbmodus der Apps",
            "How DynaTones sets the color mode of apps.", "Wie DynaTones den Farbmodus der Apps setzt.", ModeChoices),
        new Policy(WindowsColorMode, PolicyType.Choice, "Color mode of Windows", "Farbmodus von Windows",
            "How DynaTones sets the color mode of Windows: taskbar and Start.",
            "Wie DynaTones den Farbmodus von Windows setzt: Taskleiste und Start.", ModeChoices),
        new Policy(LocationMode, PolicyType.Choice, "Where the sun is calculated for", "Wofür der Sonnenstand berechnet wird",
            "The source of the position. With fixed coordinates, set them in the policy Coordinates.",
            "Die Quelle des Standorts. Bei festen Koordinaten diese in der Richtlinie Koordinaten setzen.",
            [
                new("auto", "Automatic: Windows location, then entered place, then time zone", "Automatisch: Windows-Standort, dann eingegebener Ort, dann Zeitzone"),
                new("windows", "Windows location only", "Nur Windows-Standort"),
                new("coordinates", "Fixed coordinates", "Feste Koordinaten"),
                new("timezone", "Time zone only, no location", "Nur Zeitzone, kein Standort"),
                new("none", "No sun, own times only", "Keine Sonne, nur eigene Zeiten"),
            ]),
        new Policy(Coordinates, PolicyType.Text, "Coordinates", "Koordinaten",
            "Latitude and longitude in decimal degrees with a dot, separated by a comma, for example 52.2111, 8.3557. Used when the location is set to fixed coordinates.",
            "Breite und Länge in Dezimalgrad mit Punkt, durch ein Komma getrennt, zum Beispiel 52.2111, 8.3557. Gilt, wenn der Standort auf feste Koordinaten steht."),
        new Policy(Language, PolicyType.Choice, "Language", "Sprache", "The language of DynaTones.", "Die Sprache von DynaTones.",
            [new("system", "Like Windows", "Wie Windows"), new("en", "English", "Englisch"), new("de", "German", "Deutsch")]),
        new Policy(LockScreenEnabled, PolicyType.Toggle, "Lock screen follows the day", "Sperrbildschirm folgt dem Tag",
            "Whether DynaTones changes the user's lock screen through the day, with the same images as the wallpaper unless the user picked a package of its own. The screen before signing in has a policy of its own.",
            "Ob DynaTones den Sperrbildschirm des Benutzers durch den Tag wechselt, mit denselben Bildern wie der Hintergrund, solange der Benutzer kein eigenes Paket gewählt hat. Den Bildschirm vor der Anmeldung regelt die Richtlinie 'Auch der Bildschirm vor der Anmeldung'."),
        new Policy(SignInScreen, PolicyType.Toggle, "Also the screen before signing in", "Auch der Bildschirm vor der Anmeldung",
            "Whether the screen before anyone signs in shows the lock screen's picture of the day too. Works only where DynaTones was installed with admin rights; it sets the device's lock screen picture, which Windows takes over the next time the PC is locked or starts.",
            "Ob auch der Bildschirm, bevor sich jemand anmeldet, das Sperrbild des Tages zeigt. Wirkt nur, wo DynaTones mit Adminrechten installiert wurde; es setzt das Sperrbild des Geräts, das Windows beim nächsten Sperren oder Start übernimmt."),
        new Policy(StartWithWindows, PolicyType.Toggle, "Start with Windows", "Mit Windows starten",
            "Whether DynaTones starts when the user signs in, through the user's own autostart entry.",
            "Ob DynaTones bei der Anmeldung startet, über den eigenen Autostart-Eintrag des Benutzers."),
        // The family's update policy (off, notify, install), read by KitUpdate itself.
        KitUpdate.Policy);

    /// <summary>New recommendations go into the user's settings once; returns whether anything changed.</summary>
    public static bool ApplyRecommendations(Config config)
    {
        var changed = false;
        foreach (var policy in KitPolicy.All)
        {
            if (KitPolicy.NewRecommendation(policy.Name, config.PolicyApplied) is not { } value) continue;
            changed = true;
            Set(config, policy.Name, value);
        }
        return changed;
    }

    /// <summary>Lays the fixed values over the settings; called on every read, never saved.</summary>
    public static void ApplyFixed(Config config)
    {
        foreach (var policy in KitPolicy.All)
            if (KitPolicy.Read(policy.Name, PolicyLevel.Fixed) is { } value) Set(config, policy.Name, value);
    }

    static void Set(Config config, string name, PolicyValue value)
    {
        switch (name)
        {
            case WallpaperEnabled:
                config.Wallpaper.Enabled = value.AsBool;
                break;
            case LockScreenEnabled:
                config.LockScreen.Enabled = value.AsBool;
                break;
            case SignInScreen:
                config.LockScreen.SignIn = value.AsBool;
                break;
            case ColorModeEnabled:
                config.Appearance.Enabled = value.AsBool;
                break;
            case AppsColorMode:
                config.Appearance.Apps = Mode(value.AsText);
                break;
            case WindowsColorMode:
                config.Appearance.System = Mode(value.AsText);
                break;
            case LocationMode:
                config.Location.Mode = value.AsText switch
                {
                    "windows" => Core.LocationMode.Windows,
                    "coordinates" => Core.LocationMode.Coordinates,
                    "timezone" => Core.LocationMode.TimeZone,
                    "none" => Core.LocationMode.None,
                    _ => Core.LocationMode.Auto,
                };
                break;
            case Coordinates:
                if (ParseCoordinates(value.AsText) is { } point)
                {
                    config.Location.Latitude = point.Latitude;
                    config.Location.Longitude = point.Longitude;
                    config.Location.PlaceName = null;
                }
                else
                {
                    Log.Event(KitEvents.PolicyIgnored, LogLevel.Warning, $"Policy Coordinates '{value.AsText}' is not latitude, longitude and is ignored");
                }
                break;
            case Language:
                config.Language = value.AsText == "system" ? null : value.AsText;
                break;
            case ManagedPackageFolder:
                if (ManagedPackage(value.AsText) is { } id) config.Wallpaper.PackageId = id;
                break;
            // AllowUserImport, PackageGallery and StartWithWindows are not settings in config.json: see UsersMayImport,
            // Gallery.Source, EnforceAutostart.
        }
    }

    static ModeTarget Mode(string value) => value switch
    {
        "light" => ModeTarget.AlwaysLight,
        "dark" => ModeTarget.AlwaysDark,
        "leave" => ModeTarget.Leave,
        _ => ModeTarget.Switch,
    };

    /// <summary>"52.2111, 8.3557": latitude and longitude with a dot as decimal separator.</summary>
    public static (double Latitude, double Longitude)? ParseCoordinates(string text)
    {
        var parts = text.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
            return (lat, lon);
        return null;
    }

    /// <summary>Whether users may add images and packages; false only when an organization fixed it so.</summary>
    public static bool UsersMayImport => KitPolicy.Fixed(AllowUserImport, true);

    /// <summary>Puts start at sign-in where a fixed policy wants it; called at start.</summary>
    public static void EnforceAutostart()
    {
        if (KitPolicy.Read(StartWithWindows, PolicyLevel.Fixed) is not { } value) return;
        // Installed for all users, the device's entry starts DynaTones; a user's policy cannot take it away.
        if (Autostart.ForAllUsers) return;
        if (Autostart.IsOn != value.AsBool) Autostart.Set(value.AsBool);
    }

    // ---------- The managed package ----------

    static readonly object ManagedGate = new();

    /// <summary>
    /// The package imported from the managed folder (or .zip, .ddw), imported again when its contents changed.
    /// What was imported from where is kept in managed-package.txt in the data folder.
    /// </summary>
    static string? ManagedPackage(string source)
    {
        lock (ManagedGate)
        {
            try
            {
                if (!Directory.Exists(source) && !File.Exists(source))
                {
                    Log.Event(Events.ManagedPackageMissing, LogLevel.Warning, $"Managed package {source} not found");
                    return null;
                }
                var stamp = Stamp(source);
                var record = Path.Combine(KitApp.DataFolder, "managed-package.txt");
                var lines = File.Exists(record) ? File.ReadAllLines(record) : [];
                if (lines.Length == 3 && lines[0] == source && lines[1] == stamp && Packages.Load(lines[2]) != null) return lines[2];

                // The import measures images with WPF imaging, which wants a thread of its own.
                Package? package = null;
                Exception? failure = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        package = Packages.Import(source);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                if (failure != null || package == null) throw failure ?? new InvalidOperationException("nothing imported");

                if (lines.Length == 3 && lines[2] != package.Id && Packages.Load(lines[2]) is { } old) Packages.Delete(old);
                Directory.CreateDirectory(KitApp.DataFolder);
                File.WriteAllLines(record, [source, stamp, package.Id]);
                Log.Event(Events.ManagedPackageImported, LogLevel.Info, $"Managed package imported from {source} as {package.Id}");
                return package.Id;
            }
            catch (Exception ex)
            {
                Log.Error($"Managed package {source} failed", ex, eventId: Events.ManagedPackageFailed);
                return null;
            }
        }
    }

    /// <summary>Names, sizes and times of the files: changes when the organization replaces images.</summary>
    static string Stamp(string source)
    {
        var files = Directory.Exists(source)
            ? new DirectoryInfo(source).GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            : (IEnumerable<FileInfo>)[new FileInfo(source)];
        // A stable hash: string.GetHashCode changes with every start of the process.
        var text = string.Join("|", files.Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];
    }
}
