using System.Globalization;

namespace DynaTones.Core;

/// <summary>One image of the day: from when it shows, and where the sun stands then (on the reference arc).</summary>
public sealed record Slot(DateTime Start, int Image, double Arc, bool Mirrored);

/// <summary>
/// Decides where in the day each image belongs. Every image starts at its anchor and stays until the next one
/// starts, so a package with three images covers the day as well as one with sixteen.
/// </summary>
public static class Planner
{
    // Where the parts of the day begin on the arc, the same as WinDynamicDesktop's segments: dawn from civil
    // twilight (-6°) until 6° up, day until 6° on the way down, dusk until -6°, night until dawn.
    const double TwilightLow = -6;
    const double TwilightHigh = 6;

    // ---------- Suggestions ----------

    /// <summary>
    /// Fills <see cref="PackageImage.Suggested"/> for every image. The best source wins: the time and place the
    /// photo was taken, the part of the day a WinDynamicDesktop theme names, or the look of the image together
    /// with its place in the order.
    /// </summary>
    public static void Suggest(Package package, IReadOnlyDictionary<string, ImageKind>? themeKinds = null)
    {
        var images = package.Images;
        var kinds = new ImageKind[images.Count];
        var reasons = new Said?[images.Count];
        var confidence = new double[images.Count];
        var exact = new Suggestion?[images.Count];

        for (var i = 0; i < images.Count; i++)
        {
            var img = images[i];
            if (FromPhoto(img) is { } photo)
            {
                exact[i] = photo;
                kinds[i] = photo.Kind;
            }
            else if (themeKinds != null && themeKinds.TryGetValue(img.File, out var themed))
            {
                kinds[i] = themed;
                reasons[i] = new Said("named in the WinDynamicDesktop theme", "so im WinDynamicDesktop-Theme angegeben");
                confidence[i] = 0.8;
            }
        }

        // The rest by their look, with the order deciding between morning and evening.
        var open = Enumerable.Range(0, images.Count).Where(i => exact[i] == null && reasons[i] == null).ToList();
        if (open.Count > 0) ByLook(images, open, kinds, reasons, confidence);

        // Spread each part of the day over its stretch of the arc, in file order.
        foreach (var kind in Enum.GetValues<ImageKind>())
        {
            var group = Enumerable.Range(0, images.Count).Where(i => exact[i] == null && kinds[i] == kind).ToList();
            for (var j = 0; j < group.Count; j++)
            {
                var (elevation, rising) = Spread(kind, j, group.Count);
                images[group[j]].Suggested = new Suggestion
                {
                    Elevation = Math.Round(elevation, 1),
                    Rising = rising,
                    Kind = kind,
                    Reason = reasons[group[j]]?.En ?? "",
                    ReasonDe = reasons[group[j]]?.De,
                    Confidence = confidence[group[j]],
                };
            }
        }
        for (var i = 0; i < images.Count; i++)
            if (exact[i] != null) images[i].Suggested = exact[i];
    }

    /// <summary>The <paramref name="index"/>-th of <paramref name="count"/> images of a part of the day.</summary>
    internal static (double Elevation, bool Rising) Spread(ImageKind kind, int index, int count)
    {
        var f = (double)index / count;
        switch (kind)
        {
            case ImageKind.Dawn:
                return (TwilightLow + f * (TwilightHigh - TwilightLow), true);
            case ImageKind.Dusk:
                return (TwilightHigh - f * (TwilightHigh - TwilightLow), false);
            case ImageKind.Day:
            {
                // Up from 6° to the top of the reference arc and down again.
                var half = SunDay.ReferenceHigh - TwilightHigh;
                var s = f * 2 * half;
                return s <= half ? (TwilightHigh + s, true) : (SunDay.ReferenceHigh - (s - half), false);
            }
            default:
            {
                // Down from -6° to the bottom of the reference night and up again.
                var half = TwilightLow - SunDay.ReferenceLow;
                var s = f * 2 * half;
                return s <= half ? (TwilightLow - s, false) : (SunDay.ReferenceLow + (s - half), true);
            }
        }
    }

    /// <summary>A photo that knows when and where it was taken: the sun at that moment.</summary>
    static Suggestion? FromPhoto(PackageImage image)
    {
        if (image.Look is not { TakenAt: { } taken, Latitude: { } lat, Longitude: { } lon }) return null;
        // Photos rarely store their time zone; the longitude gives one close enough for the sun.
        var zone = TimeZoneInfo.CreateCustomTimeZone("photo", TimeSpan.FromHours(Math.Round(lon / 15)), "photo", "photo");
        var day = new SunDay(DateOnly.FromDateTime(taken), lat, lon, zone);
        var elevation = day.ElevationAt(taken);
        var rising = taken <= day.SolarNoon;
        return new Suggestion
        {
            Elevation = Math.Round(day.Arc(elevation), 1),
            Rising = rising,
            Kind = KindOf(day.Arc(elevation), rising),
            Reason = $"taken at {taken:HH:mm} with the sun at {elevation.ToString("0.#", English)}°",
            ReasonDe = $"aufgenommen um {taken:HH:mm} bei {elevation.ToString("0.#", German)}° Sonnenhöhe",
            Confidence = 0.95,
        };
    }

    /// <summary>Dawn runs from -6° up to 6°, dusk from 6° down to just above -6°; -6° on the way down is night.</summary>
    static ImageKind KindOf(double arc, bool rising) =>
        arc >= TwilightHigh ? ImageKind.Day :
        rising ? (arc >= TwilightLow ? ImageKind.Dawn : ImageKind.Night) :
        (arc > TwilightLow ? ImageKind.Dusk : ImageKind.Night);

    /// <summary>
    /// Sorts images by how they look, measured against each other: a package spans the day from its darkest to its
    /// brightest image, whatever the photographer's style. The order of the files says morning or evening.
    /// </summary>
    /// <remarks>
    /// Packages made from macOS wallpapers start with the light and the dark image (macOS shows those two as the
    /// preview) and only then run through the day; that pattern is recognized. Tested against the theme files of
    /// Big Sur, The Lake, Catalina and Konstrukt: 26 of 30 images land in the part of the day the theme names; the misses are sunrise and sunset images brighter than some day images.
    /// </remarks>
    static void ByLook(List<PackageImage> images, List<int> open, ImageKind[] kinds, Said?[] reasons, double[] confidence)
    {
        static double Value(PackageImage img) => img.Look == null ? 0.3 : (img.Look.Brightness + img.Look.SkyBrightness) / 2;
        var value = open.ToDictionary(i => i, i => Value(images[i]));
        double min = value.Values.Min(), max = value.Values.Max(), range = Math.Max(max - min, 1e-6);
        double Relative(int i) => (value[i] - min) / range;

        Said Detail(int i)
        {
            var look = images[i].Look;
            return look == null ? new Said("", "") : new Said(
                $" (brightness {look.Brightness.ToString("P0", English)}, sky {(look.Warmth > 0.05 ? "warm" : look.Warmth < -0.05 ? "cool" : "neutral")})",
                $" (Helligkeit {look.Brightness.ToString("P0", German)}, Himmel {(look.Warmth > 0.05 ? "warm" : look.Warmth < -0.05 ? "kühl" : "neutral")})");
        }

        void Set(int i, ImageKind kind, Said reason, double sure)
        {
            kinds[i] = kind;
            reasons[i] = reason + Detail(i);
            confidence[i] = sure;
        }

        if (open.Count == 1)
        {
            Set(open[0], ImageKind.Day, new Said("the only image", "das einzige Bild"), 0.3);
            return;
        }

        // The macOS pattern: first the light image, then the dark one, then the day in order.
        var sequence = open;
        if (open.Count >= 5 && open[0] == 0 && open[1] == 1 && Relative(0) >= 0.6 && Relative(1) <= 0.1)
        {
            Set(0, ImageKind.Day, new Said("light image of a macOS package", "helles Bild eines macOS-Pakets"), 0.7);
            Set(1, ImageKind.Night, new Said("dark image of a macOS package", "dunkles Bild eines macOS-Pakets"), 0.7);
            sequence = open.Skip(2).ToList();
        }

        // Night: close to the darkest image of the whole package.
        var nightLimit = sequence == open ? 0.15 : 0.1;
        var nights = sequence.Where(i => Relative(i) < nightLimit).ToList();
        foreach (var i in nights) Set(i, ImageKind.Night, new Said("one of the darkest images", "eines der dunkelsten Bilder"), 0.6);
        var rest = sequence.Except(nights).ToList();
        if (rest.Count == 0) return;

        // Day: the run of images around the brightest one that stay close to it, measured within the rest.
        double rMin = rest.Min(i => value[i]), rMax = rest.Max(i => value[i]), rRange = Math.Max(rMax - rMin, 1e-6);
        bool Bright(int i) => (value[i] - rMin) / rRange >= 0.65;
        var peak = rest.OrderByDescending(i => value[i]).First();
        var at = rest.IndexOf(peak);
        int first = at, last = at;
        while (first > 0 && Bright(rest[first - 1])) first--;
        while (last < rest.Count - 1 && Bright(rest[last + 1])) last++;

        for (var k = 0; k < rest.Count; k++)
        {
            var i = rest[k];
            if (k >= first && k <= last)
                Set(i, ImageKind.Day, new Said("among the brightest images", "unter den hellsten Bildern"), 0.55);
            else if (k < first)
                Set(i, ImageKind.Dawn, new Said("twilight, morning by its place in the order", "Dämmerung, Morgen nach der Reihenfolge"), 0.45);
            else
                Set(i, ImageKind.Dusk, new Said("twilight, evening by its place in the order", "Dämmerung, Abend nach der Reihenfolge"), 0.45);
        }
    }

    /// <summary>
    /// A reason in both languages. Both are stored with the suggestion, so it reads in the language chosen when it is
    /// shown, not in the one of the import (seen 06.11.2026: German reasons in an English window).
    /// </summary>
    sealed record Said(string En, string De)
    {
        public static Said operator +(Said a, Said b) => new(a.En + b.En, a.De + b.De);
    }

    static readonly System.Globalization.CultureInfo English = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
    static readonly System.Globalization.CultureInfo German = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

    // ---------- The day ----------

    /// <summary>The anchor in effect: the own one, or the suggestion when it is on Auto.</summary>
    public static (AnchorMode Mode, double Elevation, bool Rising, TimeOnly? Time) Effective(PackageImage image)
    {
        var a = image.Anchor;
        return a.Mode switch
        {
            AnchorMode.Sun => (AnchorMode.Sun, a.Elevation, a.Rising, null),
            AnchorMode.Time when TryTime(a.Time, out var t) => (AnchorMode.Time, 0, true, t),
            AnchorMode.Off => (AnchorMode.Off, 0, true, null),
            _ => (AnchorMode.Auto, image.Suggested?.Elevation ?? 0, image.Suggested?.Rising ?? true, null),
        };
    }

    /// <summary>The images of one day in the order they show, each with its start.</summary>
    public static List<Slot> Day(Package package, SunDay sun)
    {
        var slots = new List<Slot>();
        var date = sun.Date;
        var kinds = new List<(int Image, ImageKind Kind, double Elevation, bool Rising)>();

        for (var i = 0; i < package.Images.Count; i++)
        {
            var (mode, elevation, rising, time) = Effective(package.Images[i]);
            if (mode == AnchorMode.Off) continue;
            if (time is { } t)
            {
                var start = date.ToDateTime(t);
                slots.Add(new Slot(start, i, sun.ArcAt(start), false));
                continue;
            }
            slots.Add(new Slot(Place(sun, elevation, rising), i, elevation, false));
            var kind = mode == AnchorMode.Auto && package.Images[i].Suggested is { } s ? s.Kind : KindOf(elevation, rising);
            kinds.Add((i, kind, elevation, rising));
        }

        // A package with only evening images (or only morning ones) borrows them for the other end of the day,
        // in reverse: the darkest evening image opens the morning.
        if (package.MirrorEvening)
        {
            var dawn = kinds.Where(k => k.Kind == ImageKind.Dawn).ToList();
            var dusk = kinds.Where(k => k.Kind == ImageKind.Dusk).ToList();
            var (from, to) = dawn.Count == 0 && dusk.Count > 0 ? (dusk, ImageKind.Dawn)
                : dusk.Count == 0 && dawn.Count > 0 ? (dawn, ImageKind.Dusk) : (null, ImageKind.Day);
            if (from != null)
            {
                var ordered = from.OrderBy(k => Place(sun, k.Elevation, k.Rising)).Reverse().ToList();
                for (var j = 0; j < ordered.Count; j++)
                {
                    var (elevation, rising) = Spread(to, j, ordered.Count);
                    slots.Add(new Slot(Place(sun, elevation, rising), ordered[j].Image, elevation, true));
                }
            }
        }

        return slots.OrderBy(s => s.Start).ToList();
    }

    /// <summary>
    /// When the sun reaches a point of the arc today. Where it does not get there, the image takes the nearest
    /// moment instead: a 50° image at noon in December, a deep night image at the darkest time of a June night.
    /// </summary>
    static DateTime Place(SunDay sun, double arc, bool rising)
    {
        if (sun.ArcCrossing(arc, rising) is { } t) return t;
        if (arc >= sun.ArcHighest) return rising ? sun.SolarNoon.AddSeconds(-30) : sun.SolarNoon.AddSeconds(30);
        // Lower than the sun gets tonight: the darkest moment on that side of noon.
        return rising ? DarkestBefore(sun) : DarkestAfter(sun);
    }

    static DateTime DarkestBefore(SunDay sun)
    {
        var best = 0;
        var noon = sun.IndexOf(sun.SolarNoon);
        for (var i = 1; i <= noon; i++) if (sun.ElevationAt(i) < sun.ElevationAt(best)) best = i;
        return sun.TimeAt(best);
    }

    static DateTime DarkestAfter(SunDay sun)
    {
        var noon = sun.IndexOf(sun.SolarNoon);
        var best = noon;
        for (var i = noon + 1; i < sun.Samples; i++) if (sun.ElevationAt(i) < sun.ElevationAt(best)) best = i;
        return sun.TimeAt(best);
    }

    public static bool TryTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text ?? "", ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
