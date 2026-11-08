using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>
/// A small map around a position: land, coasts and borders from Natural Earth 1:50m (public domain, built into the
/// app by tools\map.py), and a dot where DynaTones calculates the sun. Drawn here; nothing is loaded from the internet.
/// </summary>
sealed class PlaceMap : FrameworkElement
{
    sealed record Line(byte Kind, Point[] Points, Rect Box);

    static IReadOnlyList<Line>? lines;
    double latitude, longitude, span = 16;

    public PlaceMap()
    {
        Theme.Changed += InvalidateVisual;
        Unloaded += (_, _) => Theme.Changed -= InvalidateVisual;
        ClipToBounds = true;
    }

    /// <summary>Centers the map on a position; <paramref name="degrees"/> is the width shown, in degrees of longitude.</summary>
    public void Show(double lat, double lon, double degrees = 16)
    {
        latitude = lat;
        longitude = lon;
        span = degrees;
        InvalidateVisual();
    }

    static IReadOnlyList<Line> Lines => lines ??= Load();

    static IReadOnlyList<Line> Load()
    {
        var list = new List<Line>();
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DynaTones.worldmap.gz")!;
            using var unzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new BinaryReader(unzip);
            while (true)
            {
                byte kind;
                try { kind = reader.ReadByte(); }
                catch (EndOfStreamException) { break; }
                var count = reader.ReadUInt32();
                var points = new Point[count];
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                for (var i = 0; i < count; i++)
                {
                    var p = new Point(reader.ReadInt16() / 100.0, reader.ReadInt16() / 100.0);
                    points[i] = p;
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }
                list.Add(new Line(kind, points, new Rect(minX, minY, maxX - minX, maxY - minY)));
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"The map could not be read: {ex.Message}");
        }
        return list;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var dark = Theme.IsDark;
        Brush Paint(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        dc.DrawRectangle(Paint(dark ? "#1E2A33" : "#EEF4F8"), null, new Rect(0, 0, w, h));

        // Plate carrée, squeezed east to west by the latitude, so the shapes look as on a globe there.
        var squeeze = Math.Max(0.2, Math.Cos(latitude * Math.PI / 180));
        var degreesPerPixel = span / w;
        Point P(Point geo) => new(w / 2 + (geo.X - longitude) * squeeze / degreesPerPixel, h / 2 - (geo.Y - latitude) / degreesPerPixel);
        var view = new Rect(longitude - w / 2 * degreesPerPixel / squeeze, latitude - h / 2 * degreesPerPixel, w * degreesPerPixel / squeeze, h * degreesPerPixel);

        var landFill = Paint(dark ? "#2F3B2F" : "#D9E3D2");
        var coast = new Pen(Paint(dark ? "#4A5A48" : "#BFCDB6"), 1);
        var border = new Pen(Paint(dark ? "#5C6B5A" : "#A9B8A0"), 0.8) { DashStyle = new DashStyle([3, 2], 0) };
        foreach (var kind in new byte[] { 1, 2 })
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                foreach (var line in Lines)
                {
                    if (line.Kind != kind || !line.Box.IntersectsWith(view)) continue;
                    g.BeginFigure(P(line.Points[0]), kind == 1, kind == 1);
                    for (var i = 1; i < line.Points.Length; i++) g.LineTo(P(line.Points[i]), true, false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(kind == 1 ? landFill : null, kind == 1 ? coast : border, geometry);
        }

        // Where the sun is calculated.
        var center = new Point(w / 2, h / 2);
        dc.DrawEllipse(Theme.Brush("Accent"), new Pen(Brushes.White, 2), center, 6, 6);
    }
}
