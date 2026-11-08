using System.Windows;
using System.Windows.Media.Imaging;
using DynaTones.Core;

namespace DynaTones.UI;

/// <summary>
/// Small copies of the pictures for every page, read off the window's thread and kept. A page asks with
/// <see cref="Get"/>: what is ready comes back at once, anything else comes back as null and calls <c>ready</c> on the
/// window's thread when it is there, so the page draws the picture then. Decoding a 9,000 pixel picture takes long
/// enough to stall a click; nothing here ever does that on the window's thread. At most two pictures are decoded at
/// a time, so the PC stays quiet.
/// </summary>
static class ThumbCache
{
    static readonly Dictionary<string, BitmapSource?> Done = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, List<Action>> Waiting = new(StringComparer.OrdinalIgnoreCase);
    static readonly SemaphoreSlim Gate = new(2);

    /// <summary>Whether pictures are still being read; the snapshot waits for them.</summary>
    public static bool Busy => Waiting.Count > 0;

    /// <summary>The picture at about <paramref name="width"/> pixels, or null while it is read (or when it cannot be).</summary>
    public static BitmapSource? Get(string path, int width, Action? ready = null)
    {
        // Three sizes only, so the pages share their copies.
        width = width <= 160 ? 160 : width <= 320 ? 320 : 640;
        var key = $"{path}|{width}";
        if (Done.TryGetValue(key, out var image)) return image;
        if (Waiting.TryGetValue(key, out var callbacks))
        {
            if (ready != null) callbacks.Add(ready);
            return null;
        }
        Waiting[key] = ready != null ? [ready] : [];
        var dispatcher = Application.Current.Dispatcher;
        _ = Task.Run(async () =>
        {
            BitmapSource? loaded = null;
            await Gate.WaitAsync();
            try
            {
                loaded = ImageAnalysis.LoadSmall(path, width);
            }
            catch (Exception ex)
            {
                Log.Info($"Thumbnail of {Path.GetFileName(path)} failed: {ex.Message}");
            }
            finally
            {
                Gate.Release();
            }
            await dispatcher.BeginInvoke(() =>
            {
                if (Done.Count > 600) Done.Clear();
                Done[key] = loaded;
                if (Waiting.Remove(key, out var list))
                    foreach (var call in list) call();
            });
        });
        return null;
    }
}
