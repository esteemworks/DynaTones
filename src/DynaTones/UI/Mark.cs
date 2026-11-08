using System.Drawing;
using System.Drawing.Drawing2D;

namespace DynaTones.UI;

/// <summary>The DynaTones mark on the family tile: a sun on its arc above the horizon.</summary>
static class Mark
{
    public static void Paint(Graphics g, float s, Brush white)
    {
        // The arc the sun travels.
        using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(1.2f, s * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(pen, s * 0.2f, s * 0.3f, s * 0.6f, s * 0.6f, 200, 140);
        // The sun on it, to the left of the top: morning light.
        var d = s * 0.22f;
        g.FillEllipse(white, s * 0.27f - d / 2, s * 0.40f - d / 2, d, d);
        // The horizon.
        var h = Math.Max(1.5f, s * 0.08f);
        using var line = AppIcons.RoundedRect(new RectangleF(s * 0.16f, s * 0.64f, s * 0.68f, h), h / 2);
        g.FillPath(white, line);
    }
}
