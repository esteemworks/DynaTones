using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynaTones.Core;

/// <summary>
/// Looks at an image without any online service: brightness, how warm the sky is, and the time and place the photo
/// was taken if the file says so.
/// </summary>
public static class ImageAnalysis
{
    public static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"];

    public static bool IsImage(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static ImageLook Measure(string path)
    {
        var look = new ImageLook();
        try
        {
            ReadMetadata(path, look);
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading the data of {Path.GetFileName(path)} failed: {ex.Message}");
        }

        var bitmap = LoadSmall(path, 96);
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight, stride = w * 4;
        var pixels = new byte[h * stride];
        converted.CopyPixels(pixels, stride, 0);

        double all = 0, sky = 0, skyR = 0, skyB = 0, sat = 0;
        int skyRows = Math.Max(1, h / 3), count = 0, skyCount = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * stride + x * 4;
                double b = pixels[i] / 255.0, g = pixels[i + 1] / 255.0, r = pixels[i + 2] / 255.0;
                var luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                all += luma;
                sat += max <= 0 ? 0 : (max - min) / max;
                count++;
                if (y < skyRows)
                {
                    sky += luma;
                    skyR += r;
                    skyB += b;
                    skyCount++;
                }
            }
        }
        look.Brightness = all / count;
        look.SkyBrightness = sky / skyCount;
        look.Warmth = (skyR - skyB) / skyCount;
        look.Saturation = sat / count;
        return look;
    }

    /// <summary>The image decoded small: fast, and enough for colors and thumbnails.</summary>
    public static BitmapSource LoadSmall(string path, int width)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.DecodePixelWidth = width;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    static void ReadMetadata(string path, ImageLook look)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
        if (decoder.Frames.Count == 0 || decoder.Frames[0].Metadata is not BitmapMetadata meta) return;

        if (!string.IsNullOrEmpty(meta.DateTaken) &&
            DateTime.TryParse(meta.DateTaken, CultureInfo.CurrentCulture, DateTimeStyles.None, out var taken))
            look.TakenAt = taken;

        var lat = Gps(meta, "/app1/ifd/gps/{ushort=2}", "/app1/ifd/gps/{ushort=1}", 'S');
        var lon = Gps(meta, "/app1/ifd/gps/{ushort=4}", "/app1/ifd/gps/{ushort=3}", 'W');
        if (lat != null && lon != null)
        {
            look.Latitude = lat;
            look.Longitude = lon;
        }
    }

    /// <summary>Degrees, minutes and seconds stored as three rationals (numerator low, denominator high).</summary>
    static double? Gps(BitmapMetadata meta, string valueQuery, string refQuery, char negative)
    {
        try
        {
            if (!meta.ContainsQuery(valueQuery) || meta.GetQuery(valueQuery) is not ulong[] { Length: 3 } parts) return null;
            static double Rational(ulong v)
            {
                var num = (double)(v & 0xFFFFFFFF);
                var den = (double)(v >> 32);
                return den == 0 ? 0 : num / den;
            }
            var value = Rational(parts[0]) + Rational(parts[1]) / 60 + Rational(parts[2]) / 3600;
            var reference = meta.ContainsQuery(refQuery) ? meta.GetQuery(refQuery) as string : null;
            return reference != null && reference.Length > 0 && char.ToUpperInvariant(reference[0]) == negative ? -value : value;
        }
        catch
        {
            return null;
        }
    }
}
