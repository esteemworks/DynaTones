using System.Threading.Tasks;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynaTones.Core;
using Xunit;

namespace DynaTones.Tests;

public class BlendTests
{
    static readonly DateTime Start = new(2026, 11, 6, 10, 0, 0);
    static readonly DateTime End = new(2026, 11, 6, 12, 0, 0);

    [Fact]
    public void Before_the_last_minutes_the_image_shows_as_it_is()
    {
        Assert.Null(Schedule.Between(Start, End, 0, 1, 30, End.AddMinutes(-31)));
        Assert.Null(Schedule.Between(Start, End, 0, 1, 0, End.AddMinutes(-1)));
    }

    [Fact]
    public void The_way_goes_in_minute_steps_up_to_the_next_image()
    {
        var first = Schedule.Between(Start, End, 0, 1, 30, End.AddMinutes(-30))!;
        Assert.Equal((1, 30), (first.Step, first.Steps));
        Assert.Equal(End.AddMinutes(-29), first.NextStep);
        Assert.InRange(first.Amount, 0.01, 0.02);

        var last = Schedule.Between(Start, End, 0, 1, 30, End.AddSeconds(-1))!;
        Assert.Equal(30, last.Step);
        Assert.Equal(End, last.NextStep);
        Assert.InRange(last.Amount, 0.98, 0.99);
    }

    [Fact]
    public void A_short_image_is_mixed_over_its_whole_time_at_most()
    {
        var start = End.AddMinutes(-8);
        var t = Schedule.Between(start, End, 0, 1, 30, start)!;
        Assert.Equal(8, t.Steps);
        Assert.Null(Schedule.Between(End.AddMinutes(-1), End, 0, 1, 30, End.AddSeconds(-30)));
    }

    [Fact]
    public void The_plan_mixes_only_with_fade_and_different_images()
    {
        var p = new Package { Name = "Test", Folder = "x" };
        foreach (var (kind, i) in new[] { (ImageKind.Dawn, 0), (ImageKind.Day, 0), (ImageKind.Dusk, 0), (ImageKind.Night, 0) })
        {
            var (e, r) = Planner.Spread(kind, i, 1);
            p.Images.Add(new PackageImage { File = $"{p.Images.Count + 1}.jpg", Suggested = new Suggestion { Kind = kind, Elevation = e, Rising = r } });
        }
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var melle = new Position(52.211, 8.356, LocationSource.Coordinates, "Melle");
        var config = new Config();
        config.Appearance.Enabled = false;
        var probe = Schedule.Build(config, p, melle, new DateTime(2026, 11, 5, 12, 0, 0), zone);
        var at = probe.Next!.Start.AddMinutes(-10);

        var plan = Schedule.Build(config, p, melle, at, zone);
        Assert.NotNull(plan.Blend);
        Assert.Equal((probe.Current!.Image, probe.Next.Image), (plan.Blend!.From, plan.Blend.To));
        Assert.Equal(plan.Blend.NextStep, plan.NextChange);

        config.Wallpaper.Fade = false;
        Assert.Null(Schedule.Build(config, p, melle, at, zone).Blend);
    }

    [Fact]
    public async Task A_step_mixes_the_colors_and_takes_the_first_image_size()
    {
        var folder = Path.Combine(Path.GetTempPath(), "dynatones-blend-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var black = Write(Path.Combine(folder, "a.png"), 40, 20, 0);
            var white = Write(Path.Combine(folder, "b.png"), 80, 40, 255);
            var target = Path.Combine(folder, "out", "step.jpg");
            await Blend.MakeAsync(black, white, 0.5, target);

            var mixed = BitmapDecoder.Create(new Uri(target), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal((40, 20), (mixed.PixelWidth, mixed.PixelHeight));
            var pixels = new byte[40 * 4 * 20];
            new FormatConvertedBitmap(mixed, PixelFormats.Bgr32, null, 0).CopyPixels(pixels, 40 * 4, 0);
            Assert.InRange(pixels[(10 * 40 + 20) * 4], 120, 135);
            Assert.False(File.Exists(target + ".part"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    static string Write(string path, int width, int height, byte gray)
    {
        var stride = width * 4;
        var pixels = Enumerable.Repeat(gray, stride * height).ToArray();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, stride)));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
