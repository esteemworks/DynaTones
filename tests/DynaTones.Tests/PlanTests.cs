using DynaTones.Core;
using Xunit;

namespace DynaTones.Tests;

public class PlanTests
{
    static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    static readonly Position Melle = new(52.211, 8.356, LocationSource.Coordinates, "Melle");

    static Package Package(params (ImageKind Kind, int Index, int Count)[] images)
    {
        var p = new Package { Name = "Test", Folder = "x" };
        foreach (var (kind, index, count) in images)
        {
            var (e, r) = Planner.Spread(kind, index, count);
            p.Images.Add(new PackageImage { File = $"{p.Images.Count + 1}.jpg", Suggested = new Suggestion { Kind = kind, Elevation = e, Rising = r } });
        }
        return p;
    }

    [Fact]
    public void Images_follow_each_other_through_the_day()
    {
        var p = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var plan = Schedule.Build(new Config(), p, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin);
        Assert.Equal([0, 1, 2, 3], plan.TodaySlots.Select(s => s.Image));
        Assert.Equal(1, plan.Current!.Image);
        Assert.Equal(2, plan.Next!.Image);
    }

    [Fact]
    public void The_lock_screen_s_own_package_has_its_own_day_for_the_timeline()
    {
        var desktop = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var own = Package((ImageKind.Day, 0, 1), (ImageKind.Night, 0, 1));
        var config = new Config();
        config.LockScreen.Enabled = true;
        config.LockScreen.Source = LockScreenSource.Package;
        var plan = Schedule.Build(config, desktop, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin, lockPackage: own);
        Assert.Equal([0, 1], plan.LockTodaySlots.Select(s => s.Image));

        // Showing the desktop's pictures, the timeline needs no second strip.
        config.LockScreen.Source = LockScreenSource.Wallpaper;
        Assert.Empty(Schedule.Build(config, desktop, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin).LockTodaySlots);
    }

    [Fact]
    public void Shortly_after_midnight_the_night_image_of_yesterday_stays()
    {
        var p = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var plan = Schedule.Build(new Config(), p, Melle, new DateTime(2026, 11, 6, 0, 30, 0), Berlin);
        Assert.Equal(3, plan.Current!.Image);
    }

    [Fact]
    public void Evening_images_open_the_morning_when_there_is_none()
    {
        var p = Package((ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 2), (ImageKind.Dusk, 1, 2), (ImageKind.Night, 0, 1));
        var day = new SunDay(new DateOnly(2026, 11, 5), Melle.Latitude, Melle.Longitude, Berlin);
        var slots = Planner.Day(p, day);
        var mirrored = slots.Where(s => s.Mirrored).ToList();
        Assert.Equal(2, mirrored.Count);
        // The darker evening image comes first in the morning.
        Assert.Equal(2, mirrored[0].Image);
        Assert.True(mirrored.All(s => s.Start < day.SolarNoon));
    }

    [Fact]
    public void A_fixed_time_wins_over_the_sun()
    {
        var p = Package((ImageKind.Day, 0, 1), (ImageKind.Night, 0, 1));
        p.Images[1].Anchor = new Anchor { Mode = AnchorMode.Time, Time = "21:15" };
        var day = new SunDay(new DateOnly(2026, 11, 5), Melle.Latitude, Melle.Longitude, Berlin);
        var night = Planner.Day(p, day).Single(s => s.Image == 1);
        Assert.Equal(new DateTime(2026, 11, 5, 21, 15, 0), night.Start);
    }

    [Fact]
    public void A_noon_image_shows_at_noon_in_winter_too()
    {
        var p = Package((ImageKind.Night, 0, 1));
        p.Images.Add(new PackageImage { File = "noon.jpg", Anchor = new Anchor { Mode = AnchorMode.Sun, Elevation = 55, Rising = true } });
        var day = new SunDay(new DateOnly(2026, 12, 21), Melle.Latitude, Melle.Longitude, Berlin);
        var noon = Planner.Day(p, day).Single(s => s.Image == 1);
        Assert.InRange(noon.Start, day.SolarNoon.AddHours(-1.5), day.SolarNoon);
    }

    [Fact]
    public void Light_and_dark_follow_the_sun_with_offsets()
    {
        var config = new Config();
        config.Appearance.SunriseOffsetMinutes = 30;
        config.Appearance.SunsetOffsetMinutes = -45;
        var plan = Schedule.Build(config, null, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin);
        Assert.False(plan.Dark);
        Assert.Equal(new DateTime(2026, 11, 5, 16, 5, 0), plan.NextModeChange!.Value.AddSeconds(-plan.NextModeChange.Value.Second));
    }

    [Fact]
    public void Light_and_dark_can_follow_the_image()
    {
        var config = new Config();
        config.Appearance.Trigger = AppearanceTrigger.Image;
        var p = Package((ImageKind.Dawn, 0, 2), (ImageKind.Dawn, 1, 2), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 2), (ImageKind.Dusk, 1, 2), (ImageKind.Night, 0, 1));
        var morning = Schedule.Build(config, p, Melle, new DateTime(2026, 11, 5, 7, 10, 0), Berlin);
        Assert.Equal(0, morning.Current!.Image);
        Assert.True(morning.Dark);
        var noon = Schedule.Build(config, p, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin);
        Assert.False(noon.Dark);
    }

    [Fact]
    public void Polar_night_stays_dark()
    {
        var plan = Schedule.Build(new Config(), null, new Position(69.65, 18.96, LocationSource.Coordinates, "Tromsø"),
            new DateTime(2026, 12, 21, 12, 0, 0), Berlin);
        Assert.True(plan.Dark);
    }

    [Fact]
    public void AutoReasonsAreKeptInBothLanguages()
    {
        // Imported in one language, shown in the other (06.11.2026): both texts are stored.
        var p = new Package { Name = "Test", Folder = "x" };
        foreach (var brightness in new[] { 0.05, 0.3, 0.7, 0.75, 0.3, 0.06 })
            p.Images.Add(new PackageImage { File = $"{p.Images.Count + 1}.jpg", Look = new ImageLook { Brightness = brightness, SkyBrightness = brightness } });
        Planner.Suggest(p);
        Assert.All(p.Images, img =>
        {
            Assert.False(string.IsNullOrEmpty(img.Suggested!.Reason));
            Assert.False(string.IsNullOrEmpty(img.Suggested.ReasonDe));
            Assert.NotEqual(img.Suggested.Reason, img.Suggested.ReasonDe);
        });
    }

    [Fact]
    public void Lock_screen_is_off_until_switched_on()
    {
        var p = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var plan = Schedule.Build(new Config(), p, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin);
        Assert.Null(plan.LockCurrent);
        Assert.Null(plan.LockPackage);
    }

    [Fact]
    public void Lock_screen_follows_the_wallpaper()
    {
        var p = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var config = new Config();
        config.LockScreen.Enabled = true;
        var plan = Schedule.Build(config, p, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin);
        Assert.Same(p, plan.LockPackage);
        Assert.Equal(plan.Current!.Image, plan.LockCurrent!.Image);
        Assert.Equal(plan.Next!.Start, plan.LockNext!.Start);
    }

    [Fact]
    public void Lock_screen_with_a_package_of_its_own()
    {
        var desktop = Package((ImageKind.Day, 0, 1), (ImageKind.Night, 0, 1));
        var own = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var config = new Config();
        config.LockScreen.Enabled = true;
        config.LockScreen.Source = LockScreenSource.Package;
        var now = new DateTime(2026, 11, 5, 12, 0, 0);
        var plan = Schedule.Build(config, desktop, Melle, now, Berlin, lockPackage: own);
        Assert.Same(own, plan.LockPackage);
        Assert.Equal(1, plan.LockCurrent!.Image);
        // The next change of the whole plan is the earliest of the desktop, the color mode and the lock screen.
        var earliest = new[] { plan.Next?.Start, plan.NextModeChange, plan.LockNext?.Start }.Where(t => t != null).Min();
        Assert.Equal(earliest, plan.NextChange);
        Assert.True(plan.LockNext!.Start > now);
    }

    [Fact]
    public void Next_image_shows_the_coming_one_and_keeps_the_plan_a_step_ahead()
    {
        var p = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var noon = new DateTime(2026, 11, 5, 12, 0, 0);
        var plan = Schedule.Build(new Config(), p, Melle, noon, Berlin);
        var config = new Config();
        config.Wallpaper.SkipTo = plan.Next!.Start;

        var skipped = Schedule.Build(config, p, Melle, noon, Berlin);
        Assert.Equal(2, skipped.Current!.Image);
        Assert.Equal(3, skipped.Next!.Image);
        Assert.NotNull(skipped.Skipped);
        // The flowing transition before the evening image's own time does not lead back to the day image.
        var beforeDusk = plan.Next.Start.AddMinutes(-5);
        var mixing = Schedule.Build(config, p, Melle, beforeDusk, Berlin);
        Assert.Equal(2, mixing.Current!.Image);
        Assert.True(mixing.Blend == null || mixing.Blend.To != 1);
        // From the skipped image's own time on, it is the plan anyway.
        var later = Schedule.Build(config, p, Melle, plan.Next.Start.AddMinutes(1), Berlin);
        Assert.Null(later.Skipped);
        Assert.Equal(2, later.Current!.Image);
    }

    [Fact]
    public void A_skip_that_matches_no_coming_image_changes_nothing()
    {
        var p = Package((ImageKind.Dawn, 0, 1), (ImageKind.Day, 0, 1), (ImageKind.Dusk, 0, 1), (ImageKind.Night, 0, 1));
        var config = new Config();
        config.Wallpaper.SkipTo = new DateTime(2026, 11, 5, 3, 7, 0);
        var plan = Schedule.Build(config, p, Melle, new DateTime(2026, 11, 5, 12, 0, 0), Berlin);
        Assert.Null(plan.Skipped);
        Assert.Equal(1, plan.Current!.Image);
    }
}