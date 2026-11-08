using System.IO.Compression;

namespace DynaTones.Core;

/// <summary>Making and changing packages by hand: a new empty one, pictures added, moved and removed, and export.</summary>
public static partial class Packages
{
    /// <summary>A new package without pictures yet, saved, for the page "Edit package".</summary>
    public static Package CreateEmpty(string name)
    {
        var id = Slug(name) + "-" + Guid.NewGuid().ToString("N")[..6];
        var folder = Path.Combine(Paths.Packages, id);
        Directory.CreateDirectory(folder);
        var package = new Package { Name = name, Source = "new", Id = id, Folder = folder };
        Save(package);
        Log.Info($"New package \"{name}\" made");
        return package;
    }

    /// <summary>
    /// Copies pictures into a package, measures them and lets Auto place them again with the others. A file with a
    /// name the package already has gets a number. Returns how many were added.
    /// </summary>
    public static int AddImages(Package package, IEnumerable<string> files)
    {
        var added = 0;
        foreach (var file in files.Where(ImageAnalysis.IsImage))
        {
            var name = Path.GetFileName(file);
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (var n = 2; File.Exists(Path.Combine(package.Folder, name)); n++) name = $"{stem}-{n}{ext}";
            File.Copy(file, Path.Combine(package.Folder, name));
            var image = new PackageImage { File = name };
            try
            {
                image.Look = ImageAnalysis.Measure(Path.Combine(package.Folder, name));
            }
            catch (Exception ex)
            {
                Log.Warning($"Measuring {name} failed: {ex.Message}");
            }
            package.Images.Add(image);
            added++;
        }
        if (added > 0)
        {
            Planner.Suggest(package);
            Save(package);
            Log.Info($"{added} pictures added to \"{package.Name}\"");
        }
        return added;
    }

    /// <summary>Moves a picture to another place in the order of the day; Auto places them again.</summary>
    public static void Move(Package package, int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= package.Images.Count || to >= package.Images.Count) return;
        var image = package.Images[from];
        package.Images.RemoveAt(from);
        package.Images.Insert(to, image);
        Planner.Suggest(package);
        Save(package);
    }

    /// <summary>Takes a picture out of the package; its file stays until <see cref="DeleteLeftovers"/>.</summary>
    public static void Remove(Package package, PackageImage image)
    {
        package.Images.Remove(image);
        if (package.Images.Count > 0) Planner.Suggest(package);
        Save(package);
    }

    /// <summary>Puts a removed picture back at its place.</summary>
    public static void Restore(Package package, PackageImage image, int index)
    {
        package.Images.Insert(Math.Clamp(index, 0, package.Images.Count), image);
        Planner.Suggest(package);
        Save(package);
    }

    /// <summary>Deletes the files in a package's folder that no picture of it names any more.</summary>
    public static void DeleteLeftovers(Package package)
    {
        var used = package.Images.Select(i => i.File).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(package.Folder).Where(ImageAnalysis.IsImage))
        {
            if (used.Contains(Path.GetFileName(file))) continue;
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Info($"Removing {Path.GetFileName(file)} failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// The package as one .zip: its pictures and its dynatones.json with names, descriptions, times and transition, so
    /// another PC gets it as it is here (the format DynaTones reads on import).
    /// </summary>
    public static void Export(Package package, string target)
    {
        Save(package);
        var part = target + ".part";
        if (File.Exists(part)) File.Delete(part);
        using (var zip = ZipFile.Open(part, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(Path.Combine(package.Folder, ManifestName), ManifestName, CompressionLevel.Optimal);
            foreach (var image in package.Images)
            {
                var file = package.PathOf(image);
                if (File.Exists(file)) zip.CreateEntryFromFile(file, image.File, CompressionLevel.NoCompression);
            }
        }
        File.Move(part, target, true);
        Log.Info($"Package \"{package.Name}\" exported to {target}");
    }
}
