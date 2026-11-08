using System.Text;

namespace DynaTones.Core;

/// <summary>Text reports for the command line: what DynaTones would do, without doing it.</summary>
public static class Diagnostics
{
    /// <summary>Today's plan with the settings saved; optionally for another package by id or folder.</summary>
    public static string PlanReport(string? package)
    {
        var config = ConfigStore.Load();
        var pkg = package == null ? Packages.Load(config.Wallpaper.PackageId)
            : Packages.Load(package) ?? (Directory.Exists(package) ? Packages.Import(package) : null);
        var position = Task.Run(() => Locator.ResolveAsync(config.Location, askWindows: true)).GetAwaiter().GetResult();
        var plan = Schedule.Build(config, pkg, position, DateTime.Now);
        return Describe(plan, pkg, position);
    }

    /// <summary>What is on screen right now: the image of every monitor, light or dark, the pointer scheme.</summary>
    public static string StatusReport()
    {
        var sb = new StringBuilder();
        foreach (var (monitor, path) in Wallpaper.Current())
            sb.AppendLine($"Wallpaper  {path}  [{monitor}]");
        sb.AppendLine($"Apps       {(Appearance.AppsLight is { } a ? a ? "light" : "dark" : "-")}");
        sb.AppendLine($"System     {(Appearance.SystemLight is { } s ? s ? "light" : "dark" : "-")}");
        sb.AppendLine($"Pointer    {Appearance.CurrentCursorScheme ?? "-"}");
        sb.AppendLine($"Blocked    {Wallpaper.Blocked() ?? "-"}");
        sb.AppendLine($"Lock       {LockScreen.Current() ?? "-"}");
        sb.AppendLine($"LockSource {LockScreen.AppliedSource() ?? "-"}");
        sb.AppendLine($"Spotlight  {(LockScreen.SpotlightOn ? "on" : "off")}");
        sb.AppendLine($"LockBlock  {LockScreen.Blocked() ?? "-"}");
        // The screen before sign-in: set up by the admin tier, and what the SYSTEM task last wrote (edition, picture,
        // and when Windows made its own copy of it).
        sb.AppendLine($"SignIn     {(SignIn.IsInstalled ? "set up" : "not set up")}");
        if (SignIn.StatusText() is { } status)
        {
            sb.AppendLine($"SignInShown {(SignIn.NotShownYet() ? "not yet (Windows has made no copy of a device picture)" : "yes")}");
            sb.AppendLine("SignInLast " + status.Replace("\r", "").Replace("\n", " "));
        }
        return sb.ToString();
    }

    /// <summary>The settings for the diagnostics package: positions rounded to about 10 km, place name left out.</summary>
    public static string SettingsForPackage()
    {
        var config = ConfigStore.Load();
        var location = config.Location;
        if (location.Latitude is { } lat) location.Latitude = Math.Round(lat, 1);
        if (location.Longitude is { } lon) location.Longitude = Math.Round(lon, 1);
        if (location.LastWindows is { } last) location.LastWindows = last with { Latitude = Math.Round(last.Latitude, 1), Longitude = Math.Round(last.Longitude, 1) };
        if (location.PlaceName != null) location.PlaceName = "(left out)";
        return System.Text.Json.JsonSerializer.Serialize(config, Json.Options);
    }

    public static string ImportReport(string source)
    {
        var pkg = Packages.Import(source);
        var config = ConfigStore.Load();
        var position = Task.Run(() => Locator.ResolveAsync(config.Location, askWindows: true)).GetAwaiter().GetResult();
        return $"Imported as {pkg.Id}\n\n" + Describe(Schedule.Build(config, pkg, position, DateTime.Now), pkg, position);
    }

    /// <summary>The gallery as DynaTones sees it: where the list comes from and every package in it.</summary>
    public static string GalleryReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Source     {Gallery.Source ?? "off"}");
        if (Gallery.Source == null) return sb.ToString();
        var catalog = Task.Run(() => Gallery.LoadAsync()).GetAwaiter().GetResult();
        foreach (var e in catalog.Packages)
            sb.AppendLine($"  {e.Id,-18} {e.Style,-6} {(e.AiMade ? "ai   " : "drawn")} {e.Images,2} images {e.Size / 1048576.0,6:0.0} MB  " +
                          $"preview {(e.PreviewFile != null ? "yes" : "no ")}  {(Gallery.Installed(e) is { } p ? "added as " + p.Id : "-")}  {e.Name} / {e.NameDe}");
        return sb.ToString();
    }

    /// <summary>Adds one package from the gallery, as its button does; "Added as &lt;id&gt;" and the plan.</summary>
    public static string GalleryAddReport(string id)
    {
        var catalog = Task.Run(() => Gallery.LoadAsync()).GetAwaiter().GetResult();
        var entry = catalog.Packages.FirstOrDefault(e => e.Id == id) ?? throw new InvalidOperationException($"{id} is not in the gallery");
        // The import measures images with WPF imaging, which wants a thread of its own.
        var pkg = Task.Run(() => Gallery.InstallAsync(entry, UI.Sta.Run<Package>)).GetAwaiter().GetResult();
        var config = ConfigStore.Load();
        var position = Task.Run(() => Locator.ResolveAsync(config.Location, askWindows: true)).GetAwaiter().GetResult();
        return $"Added as {pkg.Id}\nName {pkg.Name} / {pkg.NameDe}\nAiMade {pkg.AiMade}\nCredits {pkg.Credits}\n\n" +
               Describe(Schedule.Build(config, pkg, position, DateTime.Now), pkg, position);
    }

    public static string Describe(Plan plan, Package? pkg, Position? position)
    {
        var sb = new StringBuilder();
        var day = plan.Today!;
        sb.AppendLine($"Now        {plan.At:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Position   {position?.Description ?? "none"} ({position?.Source.ToString() ?? "-"})");
        sb.AppendLine($"Sun        rise {Fmt(day.Crossing(Sun.SunriseElevation, true))}, noon {day.SolarNoon:HH:mm} at {day.Highest:0.0}°, set {Fmt(day.Crossing(Sun.SunriseElevation, false))}, lowest {day.Lowest:0.0}°");
        if (pkg != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Package    {pkg.Name} ({pkg.Images.Count} images)");
            for (var i = 0; i < pkg.Images.Count; i++)
            {
                var img = pkg.Images[i];
                var s = img.Suggested;
                var look = img.Look;
                sb.AppendLine($"  {i + 1,2}. {img.File,-28} {img.Anchor.Mode,-5} suggested {s?.Kind,-5} {s?.Elevation,6:0.0}° {(s?.Rising == true ? "rising " : "setting")}  {s?.Reason}" +
                              (look == null ? "" : $"  [b {look.Brightness:0.00} sky {look.SkyBrightness:0.00} warm {look.Warmth:+0.00;-0.00;0.00}]"));
            }
            sb.AppendLine();
            sb.AppendLine("Today");
            foreach (var slot in plan.TodaySlots)
                sb.AppendLine($"  from {slot.Start:HH:mm}  {pkg.Images[slot.Image].File,-28} arc {slot.Arc,6:0.0}°{(slot.Mirrored ? "  (mirrored)" : "")}");
        }
        sb.AppendLine();
        sb.AppendLine($"Current    {(plan.Current is { } c && pkg != null ? pkg.Images[c.Image].File + $" since {c.Start:dd.MM. HH:mm}" : "-")}");
        sb.AppendLine($"Next image {(plan.Next is { } n ? $"{n.Start:dd.MM. HH:mm}" : "-")}");
        if (plan.Skipped is { } skipped && pkg != null)
            sb.AppendLine($"Skipped    to {pkg.Images[skipped.Image].File} ahead of its time {skipped.Start:dd.MM. HH:mm}");
        if (plan.Blend is { } b) sb.AppendLine($"Blending   step {b.Step} of {b.Steps}, {b.Amount:P0} of the next, next step {b.NextStep:HH:mm:ss}");
        sb.AppendLine($"Mode       {(plan.Dark is { } d ? d ? "dark" : "light" : "not switched")}, next change {Fmt(plan.NextModeChange)}");
        sb.AppendLine($"Lock image {(plan.LockCurrent is { } l && plan.LockPackage is { } lp ? lp.Images[l.Image].File + $" since {l.Start:dd.MM. HH:mm}" : "-")}");
        foreach (var (at, dark) in plan.TodayModes) sb.AppendLine($"  {at:HH:mm} {(dark ? "dark" : "light")}");
        return sb.ToString();
    }

    static string Fmt(DateTime? t) => t is { } v ? v.ToString("dd.MM. HH:mm") : "-";
}
