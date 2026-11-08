using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace DynaTones.Core;

/// <summary>One package the gallery offers, as listed in packages.json.</summary>
public sealed class GalleryEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NameDe { get; set; }
    public string? Description { get; set; }
    public string? DescriptionDe { get; set; }
    /// <summary>photo, 3d, 2d or color.</summary>
    public string? Style { get; set; }
    public bool AiMade { get; set; }
    public int Images { get; set; }
    /// <summary>Bytes of the .zip.</summary>
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    /// <summary>The .zip and the preview, as file names next to packages.json.</summary>
    public string File { get; set; } = "";
    public string? Preview { get; set; }

    [JsonIgnore] public string DisplayName => Strings.German && !string.IsNullOrEmpty(NameDe) ? NameDe : Name;
    [JsonIgnore] public string? DescriptionShown => Strings.German && !string.IsNullOrEmpty(DescriptionDe) ? DescriptionDe : Description;
    /// <summary>The preview, downloaded into the cache; null when it could not be had.</summary>
    [JsonIgnore] public string? PreviewFile { get; set; }
}

public sealed class GalleryCatalog
{
    public int Format { get; set; } = 1;
    public List<GalleryEntry> Packages { get; set; } = [];
}

/// <summary>
/// More image packages to add with one click. The list, packages.json, sits next to the packages and their previews:
/// by default in the release "packages" of DynaTones on GitHub, for an organization in a folder, on a share or at a web
/// address of its own (policy PackageGallery). Every .zip is checked against the SHA-256 the list gives before it is
/// imported like any other package.
/// </summary>
public static class Gallery
{
    /// <summary>Where the list is when nothing else is set. A pre-release, so the update check never takes it for a version.</summary>
    public const string DefaultSource = "https://github.com/esteemworks/DynaTones/releases/download/packages/packages.json";

    /// <summary>
    /// The list's place: the policy, then DYNATONES_GALLERY (for tests), then GitHub; null when the organization
    /// switched the gallery off or does not let users add packages.
    /// </summary>
    public static string? Source
    {
        get
        {
            if (!Policies.UsersMayImport) return null;
            var policy = (KitPolicy.Read(Policies.PackageGallery, PolicyLevel.Fixed) ?? KitPolicy.Read(Policies.PackageGallery, PolicyLevel.Recommended))?.AsText?.Trim();
            if (string.Equals(policy, "off", StringComparison.OrdinalIgnoreCase)) return null;
            var source = !string.IsNullOrEmpty(policy) ? policy
                : Environment.GetEnvironmentVariable("DYNATONES_GALLERY") is { Length: > 0 } test ? test
                : DefaultSource;
            source = Environment.ExpandEnvironmentVariables(source);
            // A folder stands for the packages.json in it.
            return IsLocal(source) && Directory.Exists(LocalPath(source)) ? Path.Combine(LocalPath(source), "packages.json") : source;
        }
    }

    static string Folder => Path.Combine(Paths.Cache, "gallery");

    static bool IsLocal(string source) => !source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    static string LocalPath(string source) => source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(source).LocalPath : source;

    /// <summary>A file named in the list, next to the list itself. Only plain names: nothing elsewhere.</summary>
    static string Beside(string source, string name)
    {
        if (name.Length == 0 || name != Path.GetFileName(name) || name.Contains(':'))
            throw new InvalidDataException($"'{name}' is not a plain file name");
        return IsLocal(source)
            ? Path.Combine(Path.GetDirectoryName(LocalPath(source)) ?? "", name)
            : new Uri(new Uri(source), Uri.EscapeDataString(name)).ToString();
    }

    static HttpClient NewClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"DynaTones/{KitApp.Version.ToString(3)}");
        return client;
    }

    /// <summary>The list with the previews loaded into the cache. Throws when the list cannot be had.</summary>
    public static async Task<GalleryCatalog> LoadAsync(CancellationToken cancel = default)
    {
        var source = Source ?? throw new InvalidOperationException("The gallery is switched off.");
        string json;
        if (IsLocal(source)) json = await File.ReadAllTextAsync(LocalPath(source), cancel);
        else
        {
            using var client = NewClient();
            json = await client.GetStringAsync(source, cancel);
        }
        var catalog = System.Text.Json.JsonSerializer.Deserialize<GalleryCatalog>(json, Json.Options) ?? new GalleryCatalog();
        // Entries the app cannot use are left out instead of failing the whole list.
        catalog.Packages = catalog.Packages.Where(e => e.Id.Length > 0 && e.File.Length > 0 && e.File == Path.GetFileName(e.File)
            && e.File.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();

        Directory.CreateDirectory(Folder);
        foreach (var entry in catalog.Packages)
        {
            if (string.IsNullOrEmpty(entry.Preview)) continue;
            try
            {
                var local = Path.Combine(Folder, "preview-" + Path.GetFileName(entry.Preview));
                if (!File.Exists(local)) await Fetch(Beside(source, entry.Preview), local, cancel);
                entry.PreviewFile = local;
            }
            catch (Exception ex)
            {
                Log.Info($"Gallery preview {entry.Preview} could not be loaded: {ex.Message}");
            }
        }
        return catalog;
    }

    /// <summary>Whether a package from the gallery is already in DynaTones; the package, if so.</summary>
    public static Package? Installed(GalleryEntry entry) => Packages.LoadAll().FirstOrDefault(p => p.GalleryId == entry.Id);

    /// <summary>
    /// Downloads the package, checks it and imports it; the first package becomes the background, as with any import.
    /// Throws when the download fails or the file is not the one the list names.
    /// </summary>
    public static async Task<Package> InstallAsync(GalleryEntry entry, Func<Func<Package>, Task<Package>> import, CancellationToken cancel = default)
    {
        var source = Source ?? throw new InvalidOperationException("The gallery is switched off.");
        Directory.CreateDirectory(Folder);
        var file = Path.Combine(Folder, Path.GetFileName(entry.File));
        try
        {
            await Fetch(Beside(source, entry.File), file, cancel);
            await Check(file, entry.Sha256, needed: !IsLocal(source));
            var package = await import(() => Packages.Import(file));
            // The gallery's id, even when the .zip left it out, so the gallery knows the package is there.
            if (package.GalleryId != entry.Id)
            {
                package.GalleryId = entry.Id;
                Packages.Save(package);
            }
            Log.Info($"Gallery package {entry.Id} added as {package.Id}");
            return package;
        }
        finally
        {
            TryDelete(file);
        }
    }

    /// <summary>The SHA-256 the list gives. From the internet it must be there; a folder of the organization may leave it out.</summary>
    static async Task Check(string file, string expected, bool needed)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            if (needed) throw new InvalidDataException("The list gives no SHA-256 for this package");
            return;
        }
        await using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
        if (!hash.Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(Strings.T("The download is not the file the gallery names. Nothing was added.",
                "Der Download ist nicht die Datei, die die Galerie nennt. Es wurde nichts hinzugefügt."));
    }

    static async Task Fetch(string from, string to, CancellationToken cancel)
    {
        var part = to + ".part";
        try
        {
            if (IsLocal(from)) File.Copy(LocalPath(from), part, true);
            else
            {
                using var client = NewClient();
                using var response = await client.GetAsync(from, HttpCompletionOption.ResponseHeadersRead, cancel);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancel);
                await using var target = File.Create(part);
                await stream.CopyToAsync(target, cancel);
            }
            File.Move(part, to, true);
        }
        finally
        {
            TryDelete(part);
        }
    }

    static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
        }
        catch
        {
            // In the cache; goes with it.
        }
    }

    // ---------- Packages that come with the setup ----------

    /// <summary>
    /// The packages the setup brings (packages\*.zip next to the program) go into DynaTones once each; one the user
    /// deletes stays deleted. The first one becomes the background when none is chosen yet, so DynaTones shows
    /// something from the first start. Returns the id of a package made the background, or null.
    /// </summary>
    public static string? AddBundled(string programFolder)
    {
        var folder = Path.Combine(programFolder, "packages");
        if (!Directory.Exists(folder)) return null;
        var record = Path.Combine(Paths.Data, "bundled-packages.txt");
        var done = File.Exists(record) ? File.ReadAllLines(record).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
        string? first = null;
        foreach (var zip in Directory.GetFiles(folder, "*.zip").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(zip);
            if (done.Contains(name)) continue;
            try
            {
                var package = Packages.Import(zip);
                first ??= package.Id;
                Log.Info($"Package {name} from the setup added as {package.Id}");
            }
            catch (Exception ex)
            {
                Log.Warning($"Package {name} from the setup could not be added: {ex.Message}");
            }
            // Once, also when it failed: the next start does not try again and again.
            done.Add(name);
        }
        Directory.CreateDirectory(Paths.Data);
        File.WriteAllLines(record, done.Order(StringComparer.OrdinalIgnoreCase));
        return first;
    }
}
