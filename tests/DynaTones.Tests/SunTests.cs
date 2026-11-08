using DynaTones.Core;
using Xunit;

namespace DynaTones.Tests;

public class SunTests
{
    static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    const double MelleLat = 52.211, MelleLon = 8.356;

    [Fact]
    public void Sunrise_and_sunset_in_Melle_match_the_almanac()
    {
        // Reference: Auto Dark Mode's log for the same position on 05.11.2026: 07:29 and 16:50.
        var day = new SunDay(new DateOnly(2026, 11, 5), MelleLat, MelleLon, Berlin);
        Assert.Equal(new TimeOnly(7, 29), TimeOnly.FromDateTime(day.Crossing(Sun.SunriseElevation, true)!.Value).Truncate());
        Assert.Equal(new TimeOnly(16, 50), TimeOnly.FromDateTime(day.Crossing(Sun.SunriseElevation, false)!.Value).Truncate());
    }

    [Fact]
    public void Noon_height_follows_the_seasons()
    {
        // 90° - latitude ± 23.44°: about 14.4° at the winter and 61.2° at the summer solstice.
        Assert.InRange(new SunDay(new DateOnly(2026, 12, 21), MelleLat, MelleLon, Berlin).Highest, 14.0, 14.8);
        Assert.InRange(new SunDay(new DateOnly(2026, 6, 21), MelleLat, MelleLon, Berlin).Highest, 60.8, 61.6);
    }

    [Fact]
    public void A_day_with_the_clock_change_has_23_hours()
    {
        var day = new SunDay(new DateOnly(2026, 3, 29), MelleLat, MelleLon, Berlin);
        Assert.Equal(23 * 60 + 1, day.Samples);
    }

    [Fact]
    public void The_arc_keeps_the_band_and_stretches_the_rest()
    {
        var winter = new SunDay(new DateOnly(2026, 12, 21), MelleLat, MelleLon, Berlin);
        Assert.Equal(5.0, winter.Arc(5.0), 6);
        Assert.Equal(SunDay.ReferenceHigh, winter.Arc(winter.Highest), 6);
        Assert.Equal(SunDay.ReferenceLow, winter.Arc(winter.Lowest), 6);
        // A 50° image still finds its moment on a day the sun only gets to 14°.
        Assert.NotNull(winter.ArcCrossing(50, rising: true));
    }

    [Fact]
    public void Polar_night_has_no_sunrise()
    {
        var tromso = new SunDay(new DateOnly(2026, 12, 21), 69.65, 18.96, Berlin);
        Assert.Null(tromso.Crossing(Sun.SunriseElevation, true));
        Assert.True(tromso.Highest < 0);
    }

    [Fact]
    public void Iso6709_coordinates_are_read()
    {
        Assert.True(Locator.TryParseIso6709("+5230+01322", out var lat, out var lon));
        Assert.Equal(52.5, lat, 3);
        Assert.Equal(13.3667, lon, 3);
        Assert.True(Locator.TryParseIso6709("-332631-0703915", out lat, out lon));
        Assert.Equal(-33.4419, lat, 3);
        Assert.Equal(-70.6542, lon, 3);
    }

    [Fact]
    public void A_place_is_named_by_a_listed_city_only_when_it_is_near()
    {
        Assert.Equal("Berlin", Locator.Near(52.52, 13.40, 25)!.Name);
        Assert.Equal("Melle", Locator.Near(52.211, 8.356, 25)!.Name);
        // Out at sea there is no place to name.
        Assert.Null(Locator.Near(55.0, -30.0, 25));
        Assert.True(Locator.Towns.Count > 30_000);
        Assert.InRange(Locator.Distance(52.52, 13.405, 48.137, 11.575), 500, 510);
    }
}

static class TimeOnlyExtensions
{
    public static TimeOnly Truncate(this TimeOnly t) => new(t.Hour, t.Minute);
}
