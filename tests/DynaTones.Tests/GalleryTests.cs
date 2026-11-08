using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynaTones.Core;
using EsteemWorks.Kit;
using Xunit;

namespace DynaTones.Tests;

/// <summary>
/// Packages made for DynaTones bring their names and anchors; the gallery lists, checks and adds them; the setup's
/// packages come in once.
/// </summary>
[Collection("Data folder")]
public class GalleryTests : IDisposable
{
    readonly string temp = Path.Combine(Path.GetTempPath(), "dynatones-gallery-" + Guid.NewGuid().ToString("N"));
    string Data => Path.Combine(temp, "data");

    public GalleryTests()
    {
        Directory.CreateDirectory(Data);
        Environment.SetEnvironmentVariable("DYNATONES_DATA", Data);
        KitApp.Init("DynaTones", Palette.Sunset);
        Policies.Define();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DYNATONES_DATA", null);
        Environment.SetEnvironmentVariable("DYNATONES_GALLERY", null);
        try
        {
            Directory.Delete(temp, true);
        }
        catch
        {
            // Temp.
        }
    }

    /// <summary>The import measures images with WPF imaging, as the app does on a thread of its own.</summary>
    static T OnSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
        return result;
    }

    static void Picture(string path, byte gray)
    {
        var pixels = Enumerable.Repeat(gray, 32 * 18 * 3).ToArray();
        var bitmap = BitmapSource.Create(32, 18, 96, 96, PixelFormats.Rgb24, null, pixels, 32 * 3);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>A package folder with three pictures and a dynatones.json naming two of them.</summary>
    string MadeForDynaTones(string name, object[] images)
    {
        var folder = Path.Combine(temp, name);
        Directory.CreateDirectory(folder);
        OnSta(() =>
        {
            Picture(Path.Combine(folder, "1-night.jpg"), 20);
            Picture(Path.Combine(folder, "2-noon.jpg"), 230);
            Picture(Path.Combine(folder, "extra.jpg"), 128);
            return 0;
        });
        var manifest = new
        {
            name = "Mountain lake", nameDe = "Bergsee", description = "A lake.", descriptionDe = "Ein See.",
            credits = "Made with AI", aiMade = true, galleryId = "berge-foto", mirrorEvening = false, images,
        };
        File.WriteAllText(Path.Combine(folder, "dynatones.json"), JsonSerializer.Serialize(manifest));
        return folder;
    }

    [Fact]
    public void A_package_made_for_DynaTones_keeps_its_names_order_and_anchors()
    {
        var folder = MadeForDynaTones("own", [
            new { file = "2-noon.jpg", anchor = new { mode = "sun", elevation = 45, rising = true } },
            new { file = "1-night.jpg", anchor = new { mode = "time", time = "23:30" } },
        ]);
        var package = OnSta(() => Packages.Import(folder));

        Assert.Equal("Mountain lake", package.Name);
        Assert.Equal("Bergsee", package.NameDe);
        Assert.Equal("Ein See.", package.DescriptionDe);
        Assert.True(package.AiMade);
        Assert.Equal("berge-foto", package.GalleryId);
        Assert.False(package.MirrorEvening);
        // Its own order, and the picture it does not list stays out.
        Assert.Equal(["2-noon.jpg", "1-night.jpg"], package.Images.Select(i => i.File));
        Assert.Equal(AnchorMode.Sun, package.Images[0].Anchor.Mode);
        Assert.Equal(45, package.Images[0].Anchor.Elevation);
        Assert.Equal(AnchorMode.Time, package.Images[1].Anchor.Mode);
        Assert.Equal("23:30", package.Images[1].Anchor.Time);
        // Saved with it: loading it again gives the same.
        Assert.Equal("Bergsee", Packages.Load(package.Id)!.NameDe);
    }

    [Fact]
    public void An_anchor_that_makes_no_sense_goes_to_Auto()
    {
        var folder = MadeForDynaTones("odd", [
            new { file = "1-night.jpg", anchor = new { mode = "sun", elevation = 400, rising = true } },
            new { file = "2-noon.jpg", anchor = new { mode = "time", time = "25:99" } },
        ]);
        var package = OnSta(() => Packages.Import(folder));
        Assert.All(package.Images, i => Assert.Equal(AnchorMode.Auto, i.Anchor.Mode));
        Assert.All(package.Images, i => Assert.NotNull(i.Suggested));
    }

    [Fact]
    public void A_manifest_naming_files_outside_the_package_is_ignored()
    {
        var folder = MadeForDynaTones("escape", [new { file = "..\\..\\secret.jpg", anchor = new { mode = "auto" } }]);
        var package = OnSta(() => Packages.Import(folder));
        // Treated as a plain folder of images: all three, placed by Auto, named after the folder.
        Assert.Equal(3, package.Images.Count);
        Assert.Equal("escape", package.Name);
        Assert.Null(package.GalleryId);
    }

    /// <summary>A gallery in a folder: packages.json with one good package and one the app cannot use.</summary>
    (string Folder, string Sha) GalleryFolder(bool wrongHash = false)
    {
        var source = MadeForDynaTones("source", [
            new { file = "1-night.jpg", anchor = new { mode = "sun", elevation = -10, rising = false } },
            new { file = "2-noon.jpg", anchor = new { mode = "sun", elevation = 45, rising = true } },
        ]);
        var gallery = Path.Combine(temp, "gallery");
        Directory.CreateDirectory(gallery);
        var zip = Path.Combine(gallery, "berge-foto.zip");
        ZipFile.CreateFromDirectory(source, zip);
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))).ToLowerInvariant();
        OnSta(() =>
        {
            Picture(Path.Combine(gallery, "berge-foto.jpg"), 100);
            return 0;
        });
        var catalog = new
        {
            format = 1,
            packages = new object[]
            {
                new { id = "berge-foto", name = "Mountain lake", nameDe = "Bergsee", style = "photo", aiMade = true, images = 2,
                      size = new FileInfo(zip).Length, sha256 = wrongHash ? new string('0', 64) : sha, file = "berge-foto.zip", preview = "berge-foto.jpg" },
                new { id = "escape", name = "Escape", file = "..\\escape.zip", sha256 = "" },
            },
        };
        File.WriteAllText(Path.Combine(gallery, "packages.json"), JsonSerializer.Serialize(catalog));
        Environment.SetEnvironmentVariable("DYNATONES_GALLERY", gallery);
        return (gallery, sha);
    }

    [Fact]
    public async Task The_gallery_lists_usable_packages_with_their_previews()
    {
        var (folder, _) = GalleryFolder();
        Assert.Equal(Path.Combine(folder, "packages.json"), Gallery.Source);
        var catalog = await Gallery.LoadAsync();
        var entry = Assert.Single(catalog.Packages);
        Assert.Equal("berge-foto", entry.Id);
        Assert.True(File.Exists(entry.PreviewFile));
    }

    [Fact]
    public async Task A_package_from_the_gallery_is_checked_and_added_once()
    {
        GalleryFolder();
        var entry = (await Gallery.LoadAsync()).Packages[0];
        Assert.Null(Gallery.Installed(entry));
        var package = await Gallery.InstallAsync(entry, work => Task.FromResult(OnSta(work)));
        Assert.Equal("berge-foto", package.GalleryId);
        Assert.Equal(2, package.Images.Count);
        Assert.Equal(package.Id, Gallery.Installed(entry)!.Id);
        // The download is not left in the cache.
        Assert.Empty(Directory.GetFiles(Path.Combine(Paths.Cache, "gallery"), "*.zip"));
    }

    [Fact]
    public async Task A_download_that_is_not_the_named_file_adds_nothing()
    {
        GalleryFolder(wrongHash: true);
        var entry = (await Gallery.LoadAsync()).Packages[0];
        await Assert.ThrowsAsync<InvalidDataException>(() => Gallery.InstallAsync(entry, work => Task.FromResult(OnSta(work))));
        Assert.Empty(Packages.LoadAll());
    }

    [Fact]
    public void The_setup_packages_come_in_once_and_a_deleted_one_stays_deleted()
    {
        var program = Path.Combine(temp, "program");
        Directory.CreateDirectory(Path.Combine(program, "packages"));
        var source = MadeForDynaTones("lakeside", [new { file = "1-night.jpg", anchor = new { mode = "auto" } }]);
        ZipFile.CreateFromDirectory(source, Path.Combine(program, "packages", "lakeside.zip"));

        var first = OnSta(() => Gallery.AddBundled(program));
        Assert.NotNull(first);
        Assert.Single(Packages.LoadAll());

        Packages.Delete(Packages.Load(first)!);
        Assert.Null(OnSta(() => Gallery.AddBundled(program)));
        Assert.Empty(Packages.LoadAll());
    }
}
