using DynaTones.Core;
using Xunit;

namespace DynaTones.Tests;

public class ProfileTests
{
    [Fact]
    public void A_profile_changes_only_what_it_sets()
    {
        var settings = new Config();
        settings.Wallpaper.PackageId = "base";
        settings.Appearance.Apps = ModeTarget.Switch;
        var id = Guid.NewGuid();
        settings.Profiles[id] = new ProfileSettings { DesktopPackage = "other", ColorMode = ModeTarget.AlwaysDark };

        var applied = ProfileOverrides.Apply(settings, id);
        Assert.Equal("other", applied.Wallpaper.PackageId);
        Assert.Equal(ModeTarget.AlwaysDark, applied.Appearance.Apps);
        Assert.Equal(ModeTarget.AlwaysDark, applied.Appearance.System);
        Assert.Equal(settings.Wallpaper.BlendMinutes, applied.Wallpaper.BlendMinutes);
        // The settings themselves stay as they are.
        Assert.Equal("base", settings.Wallpaper.PackageId);
        Assert.Same(settings, ProfileOverrides.Apply(settings, null));
    }

    [Fact]
    public void Gentle_shows_three_steps_in_the_last_seconds()
    {
        var start = new DateTime(2026, 11, 7, 12, 0, 0);
        var end = start.AddHours(2);
        Assert.Null(Schedule.Gently(start, end, 0, 1, 6, end.AddSeconds(-7)));
        var first = Schedule.Gently(start, end, 0, 1, 6, end.AddSeconds(-6))!;
        Assert.Equal((1, 3), (first.Step, first.Steps));
        var last = Schedule.Gently(start, end, 0, 1, 6, end.AddSeconds(-1))!;
        Assert.Equal(3, last.Step);
        Assert.Equal(end, last.NextStep);
    }

    [Fact]
    public void Transition_codes_go_both_ways()
    {
        foreach (var code in new[] { -1, 0, Transitions.Gentle, 10, 30, 60 })
        {
            var t = Transitions.FromCode(code);
            Assert.Equal(code, Transitions.Code(t.Fade, t.BlendMinutes, t.BlendSeconds));
        }
    }
}
