using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DynaTones.Core;

/// <summary>
/// The image packages: each one a folder under packages with its images and a dynatones.json. Imported packages are
/// copied in, so they keep working when the source is moved or deleted.
/// </summary>
public static partial class Packages
{
    public const string ManifestName = "dynatones.json";

    public static IReadOnlyList<Package> LoadAll()
    {
        if (!Directory.Exists(Paths.Packages)) return [];
        var list = new List<Package>();
        foreach (var folder in Directory.GetDirectories(Paths.Packages))
        {
            var package = Load(Path.GetFileName(folder));
            if (package != null) list.Add(package);
        }
        return list.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static Package? Load(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var folder = Path.Combine(Paths.Packages, id);
        try
        {
            var package = Json.Read<Package>(Path.Combine(folder, ManifestName));
            if (package == null) return null;
            package.Id = id;
            package.Folder = folder;
            return package;
        }
        catch (Exception ex)
        {
            Log.Warning($"Package {id} could not be read: {ex.Message}");
            return null;
        }
    }

    public static void Save(Package package) => Json.Write(Path.Combine(package.Folder, ManifestName), package);

    public static void Delete(Package package)
    {
        if (Directory.Exists(package.Folder)) Directory.Delete(package.Folder, true);
    }

    // ---------- Import ----------

    /// <summary>A folder with images, a .zip or a WinDynamicDesktop .ddw. Suggests a place in the day for every image.</summary>
    public static Package Import(string source)
    {
        if (Directory.Exists(source)) return FromFolder(source, Path.GetFileName(source.TrimEnd('\\', '/')), "folder");

        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (ext is ".zip" or ".ddw")
        {
            // Extracted into a new, empty folder of its own; only images and the theme file are taken from it.
            var temp = Path.Combine(Paths.Cache, "import-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(temp);
            try
            {
                using (var zip = ZipFile.OpenRead(source))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.EndsWith('/')) continue;
                        var name = Path.GetFileName(entry.FullName);
                        if (!ImageAnalysis.IsImage(name) && !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                        entry.ExtractToFile(Path.Combine(temp, name), true);
                    }
                }
                return FromFolder(temp, Path.GetFileNameWithoutExtension(source), ext.TrimStart('.'));
            }
            finally
            {
                try
                {
                    Directory.Delete(temp, true);
                }
                catch
                {
                    // Cleaned up with the cache later.
                }
            }
        }
        throw new InvalidOperationException(Strings.T(
            "This is neither a folder with images nor a .zip or .ddw file.",
            "Das ist weder ein Ordner mit Bildern noch eine .zip- oder .ddw-Datei."));
    }

    static Package FromFolder(string folder, string fallbackName, string sourceKind)
    {
        // A package made for DynaTones (from the gallery, the setup or another PC) brings its names and anchors.
        var own = ReadOwnManifest(folder);
        var theme = own == null ? ReadWddTheme(folder) : null;
        var files = Directory.GetFiles(folder).Where(ImageAnalysis.IsImage).ToList();
        if (own != null)
        {
            // Its own order and only the images it lists.
            var byName = files.ToDictionary(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);
            files = own.Images.Select(i => byName.GetValueOrDefault(i.File)).OfType<string>().ToList();
        }
        else
        {
            if (theme != null)
            {
                // A WinDynamicDesktop theme names its images by number; only those belong to it.
                var listed = theme.Numbered(files);
                if (listed.Count > 0) files = listed.Select(x => x.File).ToList();
            }
            files = files.OrderBy(f => NaturalKey(Path.GetFileNameWithoutExtension(f)), StringComparer.Ordinal).ToList();
        }
        if (files.Count == 0)
            throw new InvalidOperationException(Strings.T("No images found.", "Keine Bilder gefunden."));

        var name = !string.IsNullOrWhiteSpace(own?.Name) ? own.Name.Trim() : theme?.DisplayName ?? Tidy(fallbackName);
        var id = Slug(own?.GalleryId ?? name) + "-" + Guid.NewGuid().ToString("N")[..6];
        var target = Path.Combine(Paths.Packages, id);
        Directory.CreateDirectory(target);

        var package = new Package { Name = name, Credits = own?.Credits ?? theme?.ImageCredits, Source = sourceKind, Id = id, Folder = target };
        if (own != null)
        {
            package.NameDe = own.NameDe;
            package.CreditsDe = own.CreditsDe;
            package.Description = own.Description;
            package.DescriptionDe = own.DescriptionDe;
            package.AiMade = own.AiMade;
            package.GalleryId = own.GalleryId;
            package.MirrorEvening = own.MirrorEvening;
            package.Transition = own.Transition;
        }
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(target, fileName), true);
            var image = new PackageImage { File = fileName };
            if (own?.Images.FirstOrDefault(i => i.File.Equals(fileName, StringComparison.OrdinalIgnoreCase)) is { } listed)
            {
                if (ValidAnchor(listed.Anchor)) image.Anchor = listed.Anchor;
                image.Title = listed.Title;
                image.TitleDe = listed.TitleDe;
            }
            try
            {
                image.Look = ImageAnalysis.Measure(Path.Combine(target, fileName));
            }
            catch (Exception ex)
            {
                Log.Warning($"Measuring {fileName} failed: {ex.Message}");
            }
            package.Images.Add(image);
        }

        Planner.Suggest(package, theme?.KindsByFile(files));
        Save(package);
        Log.Info($"Imported package \"{name}\" with {package.Images.Count} images from {sourceKind}");
        return package;
    }

    // ---------- DynaTones' own dynatones.json ----------

    /// <summary>The dynatones.json of a package made for DynaTones; null when there is none or it lists no images.</summary>
    static Package? ReadOwnManifest(string folder)
    {
        var file = Path.Combine(folder, ManifestName);
        if (!File.Exists(file)) return null;
        try
        {
            var package = Json.Read<Package>(file);
            // Only plain file names: nothing outside the package's own folder.
            if (package == null || package.Images.Count == 0 || package.Images.Any(i => i.File != Path.GetFileName(i.File) || i.File.Length == 0))
                return null;
            return package;
        }
        catch (Exception ex)
        {
            Log.Info($"{ManifestName} could not be read, the images are placed by Auto: {ex.Message}");
            return null;
        }
    }

    /// <summary>An anchor a package brings is taken only when it makes sense; otherwise the image goes to Auto.</summary>
    internal static bool ValidAnchor(Anchor? anchor) => anchor != null && anchor.Mode switch
    {
        AnchorMode.Sun => anchor.Elevation is >= -60 and <= 90,
        AnchorMode.Time => Planner.TryTime(anchor.Time, out _),
        _ => true,
    };

    // ---------- WinDynamicDesktop theme.json ----------

    sealed class WddTheme
    {
        public string? DisplayName { get; set; }
        public string? ImageFilename { get; set; }
        public string? ImageCredits { get; set; }
        public int[]? SunriseImageList { get; set; }
        public int[]? DayImageList { get; set; }
        public int[]? SunsetImageList { get; set; }
        public int[]? NightImageList { get; set; }

        public List<(int Number, string File)> Numbered(IEnumerable<string> files)
        {
            if (string.IsNullOrEmpty(ImageFilename) || !ImageFilename.Contains('*')) return [];
            var pattern = "^" + Regex.Escape(ImageFilename).Replace("\\*", "(\\d+)") + "$";
            var list = new List<(int, string)>();
            foreach (var file in files)
            {
                var match = Regex.Match(Path.GetFileName(file), pattern, RegexOptions.IgnoreCase);
                if (match.Success) list.Add((int.Parse(match.Groups[1].Value), file));
            }
            return list;
        }

        /// <summary>The part of the day the theme gives each image; the first list naming an image wins.</summary>
        public Dictionary<string, ImageKind> KindsByFile(IEnumerable<string> files)
        {
            var result = new Dictionary<string, ImageKind>(StringComparer.OrdinalIgnoreCase);
            foreach (var (number, file) in Numbered(files))
            {
                ImageKind? kind =
                    SunriseImageList?.Contains(number) == true ? ImageKind.Dawn :
                    DayImageList?.Contains(number) == true ? ImageKind.Day :
                    SunsetImageList?.Contains(number) == true ? ImageKind.Dusk :
                    NightImageList?.Contains(number) == true ? ImageKind.Night : null;
                if (kind != null) result[Path.GetFileName(file)] = kind.Value;
            }
            return result;
        }
    }

    static WddTheme? ReadWddTheme(string folder)
    {
        foreach (var json in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                var theme = JsonSerializer.Deserialize<WddTheme>(File.ReadAllText(json), Json.Options);
                if (theme?.ImageFilename != null) return theme;
            }
            catch (Exception ex)
            {
                Log.Info($"{Path.GetFileName(json)} is not a theme file: {ex.Message}");
            }
        }
        return null;
    }

    // ---------- Names ----------

    /// <summary>Numbers sort by value: image_2 before image_10.</summary>
    static string NaturalKey(string name) => NumberPattern().Replace(name.ToLowerInvariant(), m => m.Value.PadLeft(10, '0'));

    static string Tidy(string name) => name.Replace('_', ' ').Replace('-', ' ').Trim();

    static string Slug(string name)
    {
        var slug = SlugPattern().Replace(name.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "package" : slug[..Math.Min(slug.Length, 40)];
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
