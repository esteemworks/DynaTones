using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DynaTones.Core;

/// <summary>
/// Pointer schemes: the files of a scheme for the preview on the Color mode page, and adding a pointer package (a
/// folder or a .zip with an install.inf, or just the .cur and .ani files) as a scheme of the user. Everything stays in
/// the user's own part of Windows: the files go to DynaTones' data folder, the scheme to HKCU. No admin rights.
/// </summary>
public static partial class PointerPackages
{
    /// <summary>The roles in the order Windows lists a scheme's files.</summary>
    static readonly string[] Roles =
    [
        "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
        "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person",
    ];

    /// <summary>Words in the file names of packages without an install.inf, by role.</summary>
    static readonly string[][] Hints =
    [
        ["arrow", "normal", "pointer", "default"], ["help"], ["working", "appstarting", "background", "work"], ["busy", "wait"],
        ["cross", "precision"], ["text", "ibeam", "beam"], ["handwriting", "pen", "nwpen"], ["unavailable", "no", "not"],
        ["vert", "ns", "sizens"], ["horz", "hori", "ew", "sizewe"], ["dgn1", "nwse", "diag1"], ["dgn2", "nesw", "diag2"],
        ["move", "sizeall", "all"], ["alternate", "up", "uparrow"], ["link", "hand"], ["pin", "location"], ["person"],
    ];

    static string Folder => Path.Combine(Paths.Data, "pointers");

    /// <summary>The files of a scheme by role, environment variables expanded; empty when Windows does not know it.</summary>
    public static IReadOnlyList<string> Files(string? scheme)
    {
        string? list = null;
        if (scheme == null)
        {
            // The pointer of now: the user's current files.
            using var current = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
            return Roles.Select(r => Environment.ExpandEnvironmentVariables(current?.GetValue(r) as string ?? "")).ToList();
        }
        using (var user = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors\Schemes"))
            list = user?.GetValue(scheme) as string;
        if (list == null)
            using (var system = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes"))
                list = system?.GetValue(scheme) as string;
        return list == null ? [] : list.Split(',').Select(f => Environment.ExpandEnvironmentVariables(f.Trim())).ToList();
    }

    /// <summary>
    /// The picture of one pointer file (.cur, or the first frame of an .ani), or null when it cannot be read. An
    /// empty name means Windows' own pointer for that role, which is drawn by the caller.
    /// </summary>
    public static BitmapSource? Picture(string? file)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;
        try
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length > 4 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F') bytes = FirstAniFrame(bytes);
            if (bytes.Length < 6 || bytes[0] != 0 || bytes[1] != 0 || bytes[2] is not (1 or 2)) return null;
            // A cursor is an icon with a hot spot where the icon has planes and bit count: read as an icon.
            bytes = (byte[])bytes.Clone();
            var count = BitConverter.ToUInt16(bytes, 4);
            if (bytes[2] == 2)
            {
                bytes[2] = 1;
                for (var i = 0; i < count && 6 + 16 * i + 8 <= bytes.Length; i++)
                {
                    var at = 6 + 16 * i;
                    bytes[at + 4] = 1;
                    bytes[at + 5] = 0;
                    bytes[at + 6] = 0;
                    bytes[at + 7] = 0;
                }
            }
            using var stream = new MemoryStream(bytes);
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.OrderBy(f => Math.Abs(f.PixelWidth - 32)).First();
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            Log.Debug($"Pointer {Path.GetFileName(file)} could not be read: {ex.Message}", "Pointer");
            return null;
        }
    }

    /// <summary>The first "icon" chunk of an animated pointer (RIFF ACON).</summary>
    static byte[] FirstAniFrame(byte[] riff)
    {
        for (var i = 12; i + 8 <= riff.Length;)
        {
            var id = System.Text.Encoding.ASCII.GetString(riff, i, 4);
            var size = BitConverter.ToInt32(riff, i + 4);
            if (size < 0 || i + 8 + size > riff.Length) break;
            if (id == "icon") return riff.AsSpan(i + 8, size).ToArray();
            // A LIST chunk holds the frames: look inside it.
            i += id == "LIST" ? 12 : 8 + size + (size & 1);
        }
        return [];
    }

    // ---------- Adding a pointer package ----------

    /// <summary>
    /// Adds a pointer package from a folder or a .zip and returns the scheme's name. Only .cur, .ani and .inf files
    /// are taken, plain names, at most 2 MB each. With an install.inf its scheme name and roles count; without one the
    /// file names say which pointer is which. Throws when there are no pointers in it.
    /// </summary>
    public static string Import(string source)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        void Take(string name, Func<Stream> open, long length)
        {
            name = Path.GetFileName(name);
            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (ext is not (".cur" or ".ani" or ".inf") || length > 2 * 1024 * 1024 || files.ContainsKey(name)) return;
            using var s = open();
            using var copy = new MemoryStream();
            s.CopyTo(copy);
            files[name] = copy.ToArray();
        }
        if (Directory.Exists(source))
        {
            foreach (var f in Directory.EnumerateFiles(source, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3 }))
                Take(f, () => File.OpenRead(f), new FileInfo(f).Length);
        }
        else
        {
            using var zip = ZipFile.OpenRead(source);
            foreach (var entry in zip.Entries.Where(e => e.Length > 0))
                Take(entry.Name, entry.Open, entry.Length);
        }
        var pointers = files.Keys.Where(n => !n.EndsWith(".inf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (pointers.Count == 0)
            throw new InvalidDataException(Strings.T("There are no pointers (.cur or .ani) in it.", "Darin sind keine Mauszeiger (.cur oder .ani)."));

        var inf = files.FirstOrDefault(f => f.Key.EndsWith(".inf", StringComparison.OrdinalIgnoreCase));
        var (name, byRole) = inf.Value != null ? ReadInf(System.Text.Encoding.Default.GetString(inf.Value), pointers) : (null, null);
        name ??= Directory.Exists(source) ? Path.GetFileName(source.TrimEnd('\\', '/')) : Path.GetFileNameWithoutExtension(source);
        name = Regex.Replace(name, @"[\\/:*?""<>|,]", " ").Trim();
        if (name.Length == 0) name = "Pointer package";
        byRole ??= Guess(pointers);

        var target = Path.Combine(Folder, Regex.Replace(name, @"\s+", "-"));
        Directory.CreateDirectory(target);
        foreach (var file in pointers) File.WriteAllBytes(Path.Combine(target, file), files[file]);
        var list = string.Join(",", byRole.Select(f => f == null ? "" : Path.Combine(target, f)));
        using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors\Schemes"))
            key.SetValue(name, list, RegistryValueKind.ExpandString);
        Log.Info($"Pointer package \"{name}\" added with {pointers.Count} pointers");
        return name;
    }

    /// <summary>The scheme name and the file of each role from an install.inf: its scheme line and its strings.</summary>
    static (string? Name, string?[]? ByRole) ReadInf(string text, List<string> pointers)
    {
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "";
        string? schemeLine = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { section = line.Trim('[', ']').Trim(); continue; }
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (section.Equals("Strings", StringComparison.OrdinalIgnoreCase) && line.IndexOf('=') is > 0 and var eq)
                strings[line[..eq].Trim()] = line[(eq + 1)..].Trim().Trim('"');
            if (line.Contains(@"Cursors\Schemes", StringComparison.OrdinalIgnoreCase)) schemeLine = line;
        }
        string Expand(string s) => Regex.Replace(s, "%([^%]+)%", m => strings.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        if (schemeLine == null) return (null, null);
        // HKCU,"Control Panel\Cursors\Schemes","%SCHEME_NAME%",,"file,file,..."
        var parts = Regex.Matches(schemeLine, "\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
        if (parts.Count < 3) return (null, null);
        var name = Expand(parts[1]);
        var byRole = new string?[Roles.Length];
        var listed = Expand(parts[^1]).Split(',');
        for (var i = 0; i < Math.Min(listed.Length, Roles.Length); i++)
        {
            var file = Path.GetFileName(listed[i].Trim());
            byRole[i] = pointers.FirstOrDefault(p => p.Equals(file, StringComparison.OrdinalIgnoreCase));
        }
        return (name.Contains('%') ? null : name, byRole.Any(f => f != null) ? byRole : null);
    }

    /// <summary>Without an install.inf: each role takes the file whose name says it.</summary>
    static string?[] Guess(List<string> pointers)
    {
        var byRole = new string?[Roles.Length];
        var left = new List<string>(pointers);
        for (var i = 0; i < Roles.Length; i++)
        {
            var match = left.FirstOrDefault(p => Words(p).Intersect(Hints[i], StringComparer.OrdinalIgnoreCase).Any());
            if (match == null) continue;
            byRole[i] = match;
            left.Remove(match);
        }
        // Nothing named like a normal pointer: the first file is it.
        byRole[0] ??= left.FirstOrDefault() ?? pointers[0];
        return byRole;
    }

    static IEnumerable<string> Words(string file) =>
        Regex.Split(Path.GetFileNameWithoutExtension(file).ToLowerInvariant(), "[^a-z0-9]+").Where(w => w.Length > 0)
            .Append(Path.GetFileNameWithoutExtension(file).ToLowerInvariant());
}
