using System.Globalization;
using System.Reflection;
using Windows.Devices.Geolocation;

namespace DynaTones.Core;

public enum LocationSource { Windows, WindowsCached, Coordinates, TimeZone }

/// <summary>A position and where it came from. The accuracy says how far off the sun times can be.</summary>
public sealed record Position(double Latitude, double Longitude, LocationSource Source, string Description)
{
    /// <summary>The time zone guess can be hundreds of kilometres off; one degree of longitude is four minutes of sun.</summary>
    public bool IsRough => Source == LocationSource.TimeZone;
}

/// <summary>
/// Finds the position in the order the settings ask for. Every step works without admin rights; only the first needs
/// Windows location, and on many company devices a policy switches that off. Then the next step takes over.
/// </summary>
public static class Locator
{
    /// <summary>A position from Windows is used for a day before asking again.</summary>
    static readonly TimeSpan Fresh = TimeSpan.FromHours(24);

    public static string? LastWindowsProblem { get; private set; }

    public static async Task<Position?> ResolveAsync(LocationSettings settings, bool askWindows)
    {
        switch (settings.Mode)
        {
            case LocationMode.None:
                return null;
            case LocationMode.Coordinates:
                return Entered(settings) ?? FromTimeZone();
            case LocationMode.TimeZone:
                return FromTimeZone();
            case LocationMode.Windows:
                return await FromWindowsAsync(settings, askWindows) ?? FromTimeZone();
            default:
                return await FromWindowsAsync(settings, askWindows) ?? Entered(settings) ?? FromTimeZone();
        }
    }

    /// <summary>The same order without asking Windows: its last answer stands in. Never waits.</summary>
    public static Position? ResolveCached(LocationSettings settings)
    {
        Position? Cached() => settings.LastWindows is { } c
            ? new Position(c.Latitude, c.Longitude, LocationSource.WindowsCached, Format(c.Latitude, c.Longitude))
            : null;
        return settings.Mode switch
        {
            LocationMode.None => null,
            LocationMode.Coordinates => Entered(settings) ?? FromTimeZone(),
            LocationMode.TimeZone => FromTimeZone(),
            LocationMode.Windows => Cached() ?? FromTimeZone(),
            _ => Cached() ?? Entered(settings) ?? FromTimeZone(),
        };
    }

    static Position? Entered(LocationSettings s) =>
        s.Latitude is { } lat && s.Longitude is { } lon
            ? new Position(lat, lon, LocationSource.Coordinates, s.PlaceName ?? Format(lat, lon))
            : null;

    static async Task<Position?> FromWindowsAsync(LocationSettings settings, bool ask)
    {
        var cached = settings.LastWindows;
        if (cached != null && (!ask || DateTime.Now - cached.At < Fresh))
            return new Position(cached.Latitude, cached.Longitude, LocationSource.WindowsCached, Format(cached.Latitude, cached.Longitude));
        if (!ask) return null;

        var fresh = await AskWindowsAsync();
        if (fresh != null)
        {
            settings.LastWindows = new GeoPoint(fresh.Latitude, fresh.Longitude, DateTime.Now);
            return fresh;
        }
        return cached == null ? null
            : new Position(cached.Latitude, cached.Longitude, LocationSource.WindowsCached, Format(cached.Latitude, cached.Longitude));
    }

    /// <summary>Asks the Windows location service once, with a time limit. Null when it is off, denied or slow.</summary>
    public static async Task<Position?> AskWindowsAsync()
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            if (access != GeolocationAccessStatus.Allowed)
            {
                LastWindowsProblem = access == GeolocationAccessStatus.Denied
                    ? Strings.T("Windows location is off or not allowed for desktop apps.", "Der Windows-Standort ist aus oder für Desktop-Apps nicht erlaubt.")
                    : Strings.T("Windows did not answer the location request.", "Windows hat die Standortabfrage nicht beantwortet.");
                Log.Info($"Windows location: {access}");
                return null;
            }
            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await locator.GetGeopositionAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20));
            var point = position.Coordinate.Point.Position;
            LastWindowsProblem = null;
            // Rounded to about 10 km: enough for sun times, not a home address in a log someone passes on.
            Log.Info(FormattableString.Invariant($"Windows location: {point.Latitude:0.0}, {point.Longitude:0.0} ({position.Coordinate.PositionSource})"));
            return new Position(point.Latitude, point.Longitude, LocationSource.Windows, Format(point.Latitude, point.Longitude));
        }
        catch (Exception ex)
        {
            LastWindowsProblem = ex is TimeoutException
                ? Strings.T("Windows location took too long.", "Der Windows-Standort hat zu lange gebraucht.")
                : Strings.T("Windows location is not available here.", "Der Windows-Standort ist hier nicht verfügbar.");
            Log.Warning($"Windows location failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // ---------- The time zone as a last resort ----------

    /// <summary>
    /// The principal place of the Windows time zone, for the country set in Windows where the zone covers several.
    /// Always there, needs no rights and no internet; for Melle it lands in Berlin, about 20 minutes of sun away.
    /// </summary>
    public static Position? FromTimeZone()
    {
        try
        {
            var region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            var id = TimeZoneInfo.Local.Id;
            string? iana = null;
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, region, out var regional)) iana = regional;
            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var general)) iana = general;
            if (iana == null) return null;
            var zone = Zones.FirstOrDefault(z => z.Zone == iana);
            if (zone == null) return null;
            return new Position(zone.Latitude, zone.Longitude, LocationSource.TimeZone, zone.City);
        }
        catch (Exception ex)
        {
            Log.Warning($"Time zone position failed: {ex.Message}");
            return null;
        }
    }

    public sealed record Place(string Country, double Latitude, double Longitude, string Zone)
    {
        public string City => Zone[(Zone.LastIndexOf('/') + 1)..].Replace('_', ' ');
        public override string ToString() => $"{City} ({Country})";
    }

    /// <summary>The principal places of all time zones, from tzdb. Also the place list to pick from.</summary>
    public static IReadOnlyList<Place> Zones => zones ??= LoadZones();
    static IReadOnlyList<Place>? zones;

    static IReadOnlyList<Place> LoadZones()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DynaTones.zones.tab")!;
        using var reader = new StreamReader(stream);
        var list = new List<Place>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split('\t');
            if (parts.Length < 3 || !TryParseIso6709(parts[1], out var lat, out var lon)) continue;
            list.Add(new Place(parts[0], lat, lon, parts[2]));
        }
        return list;
    }

    /// <summary>±DDMM±DDDMM or ±DDMMSS±DDDMMSS.</summary>
    internal static bool TryParseIso6709(string text, out double latitude, out double longitude)
    {
        latitude = longitude = 0;
        var split = text.IndexOfAny(['+', '-'], 1);
        if (split < 0) return false;
        return TryPart(text[..split], 2, out latitude) && TryPart(text[split..], 3, out longitude);

        static bool TryPart(string part, int degreeDigits, out double value)
        {
            value = 0;
            var sign = part[0] == '-' ? -1 : 1;
            var digits = part[1..];
            if (digits.Length < degreeDigits + 2) return false;
            var deg = int.Parse(digits[..degreeDigits], CultureInfo.InvariantCulture);
            var min = int.Parse(digits.Substring(degreeDigits, 2), CultureInfo.InvariantCulture);
            var sec = digits.Length >= degreeDigits + 4 ? int.Parse(digits.Substring(degreeDigits + 2, 2), CultureInfo.InvariantCulture) : 0;
            value = sign * (deg + min / 60.0 + sec / 3600.0);
            return true;
        }
    }

    /// <summary>A place with at least 15,000 inhabitants, from GeoNames (CC BY 4.0, see tools\cities.py).</summary>
    public sealed record Town(string Name, string Country, double Latitude, double Longitude, int People)
    {
        /// <summary>The country's name in the language of Windows, or its code when Windows does not know it.</summary>
        public string CountryName
        {
            get
            {
                try
                {
                    return new RegionInfo(Country).DisplayName;
                }
                catch (ArgumentException)
                {
                    return Country;
                }
            }
        }

        /// <summary>The country only where it is not the one of this PC: "Melle", but "Enschede, Niederlande".</summary>
        public string Label => Country.Equals(Home, StringComparison.OrdinalIgnoreCase) ? Name : $"{Name}, {CountryName}";

        public override string ToString() => Label;
    }

    static string Home => home ??= SafeRegion();
    static string? home;

    static string SafeRegion()
    {
        try
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// The places DynaTones knows by name, largest first. Read on first use only, never at start: naming a place is
    /// for the window, the sun needs only the coordinates.
    /// </summary>
    public static IReadOnlyList<Town> Towns => towns ??= LoadTowns();
    static IReadOnlyList<Town>? towns;

    static IReadOnlyList<Town> LoadTowns()
    {
        var list = new List<Town>(32_000);
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DynaTones.cities.tsv.gz")!;
            using var unzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(unzip, System.Text.Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split('\t');
                if (parts.Length < 5
                    || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                    || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;
                int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var people);
                list.Add(new Town(parts[0], parts[1], lat, lon, people));
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Place names could not be read: {ex.Message}");
        }
        return list;
    }

    /// <summary>The place nearest to a point, when it is within <paramref name="kilometres"/>; else null.</summary>
    public static Town? Near(double latitude, double longitude, double kilometres)
    {
        Town? best = null;
        var bestKm = double.MaxValue;
        // A quick box first: one degree of latitude is 111 km, so most places are left out without trigonometry.
        var box = kilometres / 111.0 + 0.01;
        var lonBox = box / Math.Max(0.05, Math.Cos(latitude * Math.PI / 180));
        foreach (var t in Towns)
        {
            if (Math.Abs(t.Latitude - latitude) > box) continue;
            var dLon = Math.Abs(t.Longitude - longitude);
            if (Math.Min(dLon, 360 - dLon) > lonBox) continue;
            var km = Distance(latitude, longitude, t.Latitude, t.Longitude);
            if (km < bestKm)
            {
                best = t;
                bestKm = km;
            }
        }
        return bestKm <= kilometres ? best : null;
    }

    /// <summary>Great-circle distance in kilometres.</summary>
    internal static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lon2 - lon1) / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static string Format(double latitude, double longitude) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.00}° {1}, {2:0.00}° {3}",
            Math.Abs(latitude), latitude >= 0 ? "N" : "S", Math.Abs(longitude), longitude >= 0 ? "E" : "W");
}
