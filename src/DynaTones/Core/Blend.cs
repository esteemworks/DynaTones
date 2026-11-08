using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynaTones.Core;

/// <summary>
/// The images between two wallpapers: the same place in other light, mixed in small steps, so the background flows
/// from one to the next instead of jumping. Each step is a file of its own, because Windows takes a background by its
/// path and keeps showing an old picture when the path stays the same.
/// </summary>
public static class Blend
{
    static string Folder => Path.Combine(Paths.Data, "blend");

    /// <summary>
    /// The two pictures of a transition, made smaller to the screen once: the 30 steps of a flowing transition read
    /// these instead of decoding a 9,000 pixel original twice per step (measured 07.11.2026: most of a step's time).
    /// </summary>
    static string SourceFolder => Path.Combine(Paths.Cache, "blend-source");

    /// <summary>
    /// The file of one step. The name holds the two images, their dates and the step, so the same step of the same
    /// images is the same file, and a changed image gives new ones.
    /// </summary>
    public static string FramePath(string from, string to, Transition t)
    {
        var key = $"{Path.GetFullPath(from).ToLowerInvariant()}|{File.GetLastWriteTimeUtc(from).Ticks}|{Path.GetFullPath(to).ToLowerInvariant()}|{File.GetLastWriteTimeUtc(to).Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12].ToLowerInvariant();
        return Path.Combine(Folder, $"{hash}-{t.Step:00}-{t.Steps:00}.jpg");
    }

    /// <summary>
    /// Mixes the two images and writes the step. Runs on a thread of its own: WPF's image classes want one, and the
    /// window stays responsive. The second image is scaled to the first one's size when they differ.
    /// </summary>
    /// <param name="screen">
    /// The pixels the image has to cover; larger images are made smaller to it while they are read, which keeps a 9K
    /// photo from taking a gigabyte for a picture no screen shows in full. Null keeps the size.
    /// </param>
    public static Task MakeAsync(string from, string to, double amount, string target, (int Width, int Height)? screen = null)
    {
        var done = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                Make(from, to, amount, target, screen);
                done.SetResult();
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
            finally
            {
                // The image classes start a dispatcher on this thread; end it so nothing is left behind.
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                // A step of a large image takes a few hundred megabytes for a moment; give them back to Windows right
                // away instead of keeping them until the next one.
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            }
        }) { IsBackground = true, Name = "DynaTones blend", Priority = ThreadPriority.BelowNormal };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    static void Make(string from, string to, double amount, string target, (int Width, int Height)? screen)
    {
        // The size of the result: the first image's, made smaller until it just covers the screen.
        var (width, height) = Size(from);
        if (screen is { Width: > 0, Height: > 0 } s)
        {
            var scale = Math.Max((double)s.Width / width, (double)s.Height / height);
            if (scale < 1)
            {
                width = Math.Max(1, (int)Math.Round(width * scale));
                height = Math.Max(1, (int)Math.Round(height * scale));
            }
        }
        var stride = width * 4;
        var first = Source(from, width, height);
        var second = Source(to, width, height);

        // Fixed point: 256 parts, done row by row in parallel. A 5K image takes well under a second.
        var share = (int)Math.Round(Math.Clamp(amount, 0, 1) * 256);
        var keep = 256 - share;
        // Two cores at most: a step is no hurry, the PC's other work is.
        Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = 2 }, y =>
        {
            var end = (y + 1) * stride;
            for (var i = y * stride; i < end; i++)
                first[i] = (byte)((first[i] * keep + second[i] * share) >> 8);
        });

        var mixed = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, first, stride);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(mixed));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        // Written beside and then moved, so Windows never gets half a file.
        var part = target + ".part";
        using (var stream = File.Create(part))
            encoder.Save(stream);
        File.Move(part, target, overwrite: true);
    }

    /// <summary>
    /// A picture's pixels at this size (BGR32): read straight from the cache when it was made smaller before, else
    /// decoded, made smaller and kept as raw pixels, which read back without decoding at all.
    /// </summary>
    static byte[] Source(string path, int width, int height)
    {
        var key = $"{Path.GetFullPath(path).ToLowerInvariant()}|{File.GetLastWriteTimeUtc(path).Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12].ToLowerInvariant();
        var cached = Path.Combine(SourceFolder, $"{hash}-{width}x{height}.bgr32");
        var size = width * 4 * height;
        if (File.Exists(cached) && new FileInfo(cached).Length == size)
        {
            try
            {
                return File.ReadAllBytes(cached);
            }
            catch (Exception ex)
            {
                Log.Debug($"Cached source {Path.GetFileName(cached)} unreadable, made again: {ex.Message}", "Engine");
            }
        }
        var pixels = new byte[size];
        Load(path, width, height).CopyPixels(pixels, width * 4, 0);
        try
        {
            Directory.CreateDirectory(SourceFolder);
            var part = cached + ".part";
            File.WriteAllBytes(part, pixels);
            File.Move(part, cached, true);
            // The pictures of the last two transitions are enough.
            foreach (var old in new DirectoryInfo(SourceFolder).GetFiles().OrderByDescending(f => f.LastWriteTimeUtc).Skip(4))
                old.Delete();
        }
        catch (Exception ex)
        {
            Log.Debug($"Keeping the smaller picture failed: {ex.Message}", "Engine");
        }
        return pixels;
    }

    /// <summary>The pixel size, read from the file's header only.</summary>
    static (int Width, int Height) Size(string path)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    /// <summary>Reads the image at exactly this size; a JPEG is made smaller while it is decoded, which is fast.</summary>
    static BitmapSource Load(string path, int width, int height)
    {
        var image = new BitmapImage();
        using (var stream = File.OpenRead(path))
        {
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = width;
            image.DecodePixelHeight = height;
            image.EndInit();
        }
        BitmapSource result = image;
        // Some formats ignore the decode size; then it is scaled here.
        if (result.PixelWidth != width || result.PixelHeight != height)
            result = new TransformedBitmap(result, new ScaleTransform((double)width / result.PixelWidth, (double)height / result.PixelHeight));
        return new FormatConvertedBitmap(result, PixelFormats.Bgr32, null, 0);
    }

    /// <summary>Removes the steps no longer shown; the one on screen and the one before stay for Windows' fade.</summary>
    public static void Clean(params string?[] keep)
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            foreach (var file in Directory.GetFiles(Folder))
            {
                if (keep.Any(k => Wallpaper.SamePath(k, file))) continue;
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // Still in use: next time.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Cleaning the blend folder failed: {ex.Message}", "Engine");
        }
    }

    /// <summary>Whether the path is one of the steps, for telling them apart from the images of a package.</summary>
    public static bool IsStep(string? path) =>
        !string.IsNullOrEmpty(path) && Path.GetFullPath(path).StartsWith(Path.GetFullPath(Folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
