using System.IO;
using DynaTones.Core;
using EsteemWorks.Kit;
using Xunit;

namespace DynaTones.Tests;

/// <summary>
/// Restorable settings (kit STANDARD.md, section 3): the settings of every released version are taken over, and
/// export and import bring them back. tests\fixtures holds config.json as each released version wrote it.
/// </summary>
[Collection("Data folder")]
public class SettingsTests : IDisposable
{
    readonly string temp = Path.Combine(Path.GetTempPath(), "dynatones-settings-" + Guid.NewGuid().ToString("N"));

    public SettingsTests()
    {
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("DYNATONES_DATA", temp);
        KitApp.Init("DynaTones", Palette.Sunset);
        Policies.Define();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DYNATONES_DATA", null);
        try
        {
            Directory.Delete(temp, true);
        }
        catch
        {
            // Temp.
        }
    }

    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Version_1_settings_are_read_as_they_were_saved()
    {
        File.Copy(Fixture("config-v1.json"), Paths.Config);
        KitSettings.Configure(Config.Schema, ["config.json"]);
        var config = ConfigStore.Load();
        Assert.Equal(Config.Schema, config.Version);
        Assert.Equal("en", config.Language);
        Assert.Equal(LocationMode.Coordinates, config.Location.Mode);
        Assert.Equal("Hamburg", config.Location.PlaceName);
        Assert.Equal("lakeside-1-2f6a1c", config.Wallpaper.PackageId);
        Assert.Equal(30, config.Wallpaper.BlendMinutes);
        Assert.Equal(-45, config.Appearance.SunsetOffsetMinutes);
        Assert.True(config.LockScreen.Enabled);
        Assert.Null(KitSettings.Problem);
        // Saved before there was a choice of accent: the default, the color of the profile the window shows.
        Assert.Equal(AccentChoice.Shown, config.Accent);
    }

    [Fact]
    public void The_accent_is_kept_as_chosen()
    {
        File.Copy(Fixture("config-v1.json"), Paths.Config);
        KitSettings.Configure(Config.Schema, ["config.json"]);
        foreach (var accent in new[] { AccentChoice.Brand, AccentChoice.Windows, AccentChoice.Sand, AccentChoice.Shown })
        {
            ConfigStore.Update(c => c.Accent = accent);
            Assert.Equal(accent, ConfigStore.Load().Accent);
        }
        // The rest of the old settings stays as it was.
        Assert.Equal("Hamburg", ConfigStore.Load().Location.PlaceName);
    }

    [Fact]
    public void An_export_brings_the_settings_back_after_a_change()
    {
        File.Copy(Fixture("config-v1.json"), Paths.Config);
        KitSettings.Configure(Config.Schema, ["config.json"]);
        var zip = Path.Combine(temp, "export.zip");
        KitSettings.Export(zip);

        ConfigStore.Update(c => c.Wallpaper.BlendMinutes = 60);
        Assert.Equal(60, ConfigStore.Load().Wallpaper.BlendMinutes);

        var result = KitSettings.Import(zip);
        Assert.True(result.Ok, result.Message);
        Assert.Equal(30, ConfigStore.Load().Wallpaper.BlendMinutes);
        // The image packages are not settings: the export holds config.json only.
        using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
        Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith("packages", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_unreadable_file_is_kept_and_reported_not_replaced_silently()
    {
        File.WriteAllText(Paths.Config, "{ this is not json");
        KitSettings.Configure(Config.Schema, ["config.json"]);
        var config = ConfigStore.Load();
        Assert.NotNull(config);
        Assert.NotNull(KitSettings.Problem);
        Assert.Contains(Directory.GetFiles(KitSettings.BackupFolder), f => f.Contains("unreadable"));
    }
}
