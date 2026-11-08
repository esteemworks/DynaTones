namespace DynaTones.Core;

/// <summary>
/// Where the sun stands, computed on the device (NOAA solar position algorithm, about 0.01° accurate for our years).
/// Elevations are geometric, without refraction; sunrise and sunset are at -0.833°, as almanacs count them.
/// </summary>
public static class Sun
{
    public const double SunriseElevation = -0.833;

    public static (double Elevation, double Azimuth) Position(DateTime utc, double latitude, double longitude)
    {
        if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
        var jd = utc.ToOADate() + 2415018.5;
        var t = (jd - 2451545.0) / 36525.0;

        var l0 = Mod(280.46646 + t * (36000.76983 + t * 0.0003032), 360);
        var m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
        var e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
        var c = Sin(m) * (1.914602 - t * (0.004817 + 0.000014 * t)) + Sin(2 * m) * (0.019993 - 0.000101 * t) + Sin(3 * m) * 0.000289;
        var omega = 125.04 - 1934.136 * t;
        var lambda = l0 + c - 0.00569 - 0.00478 * Sin(omega);
        var eps0 = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60;
        var eps = eps0 + 0.00256 * Cos(omega);
        var declination = Deg(Math.Asin(Sin(eps) * Sin(lambda)));

        var y = Math.Pow(Math.Tan(Rad(eps / 2)), 2);
        var equationOfTime = 4 * Deg(y * Sin(2 * l0) - 2 * e * Sin(m) + 4 * e * y * Sin(m) * Cos(2 * l0)
            - 0.5 * y * y * Sin(4 * l0) - 1.25 * e * e * Sin(2 * m));

        var minutes = utc.TimeOfDay.TotalMinutes;
        var trueSolarTime = Mod(minutes + equationOfTime + 4 * longitude, 1440);
        var hourAngle = trueSolarTime / 4 - 180;

        var cosZenith = Sin(latitude) * Sin(declination) + Cos(latitude) * Cos(declination) * Cos(hourAngle);
        var zenith = Deg(Math.Acos(Math.Clamp(cosZenith, -1, 1)));
        var elevation = 90 - zenith;

        var sinZenith = Sin(zenith);
        double azimuth;
        if (Math.Abs(sinZenith) < 1e-9 || Math.Abs(Cos(latitude)) < 1e-9) azimuth = 180;
        else
        {
            var cosAz = (Sin(latitude) * Cos(zenith) - Sin(declination)) / (Cos(latitude) * sinZenith);
            var az = Deg(Math.Acos(Math.Clamp(cosAz, -1, 1)));
            azimuth = hourAngle > 0 ? Mod(az + 180, 360) : Mod(540 - az, 360);
        }
        return (elevation, azimuth);
    }

    static double Sin(double degrees) => Math.Sin(Rad(degrees));
    static double Cos(double degrees) => Math.Cos(Rad(degrees));
    static double Rad(double degrees) => degrees * Math.PI / 180;
    static double Deg(double radians) => radians * 180 / Math.PI;
    static double Mod(double value, double by) => ((value % by) + by) % by;
}

/// <summary>
/// One local day of the sun at a place: the elevation every minute, the highest and lowest point, and the times the
/// sun passes a given elevation on its way up or down. Built for a calendar day in a time zone, so days with a
/// daylight saving change have 23 or 25 hours.
/// </summary>
public sealed class SunDay
{
    public DateOnly Date { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public TimeZoneInfo Zone { get; }

    /// <summary>Instants of the samples, as local times of <see cref="Zone"/>.</summary>
    readonly DateTime[] times;
    readonly double[] elevations;
    readonly int noon;
    readonly int troughBefore;
    readonly int troughAfter;

    public SunDay(DateOnly date, double latitude, double longitude, TimeZoneInfo? zone = null)
    {
        Date = date;
        Latitude = latitude;
        Longitude = longitude;
        Zone = zone ?? TimeZoneInfo.Local;

        var startUtc = ToUtc(date.ToDateTime(TimeOnly.MinValue));
        var endUtc = ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var count = (int)Math.Round((endUtc - startUtc).TotalMinutes) + 1;
        times = new DateTime[count];
        elevations = new double[count];
        for (var i = 0; i < count; i++)
        {
            var utc = startUtc.AddMinutes(i);
            times[i] = TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
            elevations[i] = Sun.Position(utc, latitude, longitude).Elevation;
        }

        noon = 0;
        for (var i = 1; i < count; i++) if (elevations[i] > elevations[noon]) noon = i;
        troughBefore = 0;
        for (var i = 1; i <= noon; i++) if (elevations[i] < elevations[troughBefore]) troughBefore = i;
        troughAfter = noon;
        for (var i = noon + 1; i < count; i++) if (elevations[i] < elevations[troughAfter]) troughAfter = i;
    }

    DateTime ToUtc(DateTime local)
    {
        // A local midnight that does not exist (rare zones switch at midnight) moves on by an hour.
        while (Zone.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    }

    public DateTime Start => times[0];
    public DateTime End => times[^1];
    public DateTime SolarNoon => times[noon];
    public double Highest => elevations[noon];
    public double Lowest => Math.Min(elevations[troughBefore], elevations[troughAfter]);
    public int Samples => times.Length;
    public DateTime TimeAt(int index) => times[index];
    public double ElevationAt(int index) => elevations[index];
    public bool RisingAt(int index) => index <= noon;

    public double ElevationAt(DateTime local)
    {
        var index = IndexOf(local);
        return elevations[index];
    }

    public int IndexOf(DateTime local)
    {
        var i = (int)Math.Round((local - times[0]).TotalMinutes);
        return Math.Clamp(i, 0, times.Length - 1);
    }

    /// <summary>
    /// When the sun passes <paramref name="elevation"/> on its way up (rising) or down. Null when it does not get
    /// there today: then it stays above or below it all day long.
    /// </summary>
    public DateTime? Crossing(double elevation, bool rising) =>
        rising ? Find(troughBefore, noon, elevation, rising: true) : Find(noon, troughAfter, elevation, rising: false);

    /// <summary>Like <see cref="Crossing"/> for a value of <see cref="Arc"/> instead of a raw elevation.</summary>
    public DateTime? ArcCrossing(double arc, bool rising)
    {
        var from = rising ? troughBefore : noon;
        var to = rising ? noon : troughAfter;
        for (var i = from; i < to; i++)
        {
            var a = Arc(elevations[i]);
            var b = Arc(elevations[i + 1]);
            if (rising ? a <= arc && b > arc : a >= arc && b < arc)
                return Interpolate(i, a, b, arc);
        }
        return null;
    }

    DateTime? Find(int from, int to, double target, bool rising)
    {
        for (var i = from; i < to; i++)
        {
            var a = elevations[i];
            var b = elevations[i + 1];
            if (rising ? a <= target && b > target : a >= target && b < target)
                return Interpolate(i, a, b, target);
        }
        return null;
    }

    DateTime Interpolate(int i, double a, double b, double target)
    {
        var f = Math.Abs(b - a) < 1e-12 ? 0 : (target - a) / (b - a);
        return times[i].AddSeconds(Math.Round(60 * f));
    }

    // ---------- The arc: elevations brought onto one shape for every day of the year ----------

    /// <summary>Near the horizon the real elevation counts: twilight and golden hour look alike all year.</summary>
    public const double BandTop = 10;
    public const double BandBottom = -12;
    /// <summary>The highest point of the reference day. Above the band, today's part of the arc is stretched onto it.</summary>
    public const double ReferenceHigh = 60;
    /// <summary>The lowest point of the reference night.</summary>
    public const double ReferenceLow = -40;

    /// <summary>
    /// An elevation of today on the reference arc. Inside the band from -12° to +10° nothing changes. Above it, the
    /// way from the band to today's highest point is stretched onto the way to 60°; below it, the way down to today's
    /// lowest point onto the way to -40°. A noon image meant for 50° then shows at noon in December too, when the
    /// sun only gets to 14°.
    /// </summary>
    public double Arc(double elevation)
    {
        if (elevation > BandTop)
            return Highest > BandTop + 0.5 ? BandTop + (elevation - BandTop) * (ReferenceHigh - BandTop) / (Highest - BandTop) : elevation;
        if (elevation < BandBottom)
            return Lowest < BandBottom - 0.5 ? BandBottom - (BandBottom - elevation) * (BandBottom - ReferenceLow) / (BandBottom - Lowest) : elevation;
        return elevation;
    }

    public double ArcHighest => Arc(Highest);
    public double ArcLowest => Arc(Lowest);
    public double ArcAt(DateTime local) => Arc(ElevationAt(local));
}
