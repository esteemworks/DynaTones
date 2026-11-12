using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynaTones.Core;

namespace DynaTones.UI;

/// <summary>
/// One day at a glance: the sun's arc with the horizon, a band for light and dark, the images as a strip below it in
/// the order they show, and a line for now. When the lock screen has a package of its own, a second strip shows its
/// images, each strip marked with its symbol in the margin. Drawn by hand from the theme tokens.
/// </summary>
public sealed class DayTimeline : FrameworkElement
{
    Plan? plan;
    Package? package;
    bool showNow = true;

    const double Left = 112, Right = 16, Top = 14, ModeHeight = 8, AxisHeight = 22, StripGap = 6;
    double CurveHeight = 120, StripHeight = 64;

    public DayTimeline()
    {
        Fit();
        Theme.Changed += InvalidateVisual;
    }

    /// <summary>The lock screen's own strip, when it has a package of its own.</summary>
    bool TwoStrips => plan?.LockPackage != null && plan.LockTodaySlots.Count > 0;

    void Fit() => Height = Top + CurveHeight + 10 + ModeHeight + 8 + StripHeight + (TwoStrips ? StripGap + StripHeight : 0) + AxisHeight;

    /// <summary>A flatter day for the overview, where the pictures of now share the space.</summary>
    public void Compact(double curve = 56, double strip = 36)
    {
        CurveHeight = curve;
        StripHeight = strip;
        Fit();
        InvalidateVisual();
    }

    /// <summary>Shows a plan; <paramref name="isToday"/> false hides the line for now (another date).</summary>
    public void Show(Plan? value, Package? pkg, bool isToday)
    {
        plan = value;
        package = pkg;
        showNow = isToday;
        Fit();
        InvalidateVisual();
    }

    double Width0 => Math.Max(10, ActualWidth - Left - Right);

    double X(DateTime t)
    {
        var day = plan!.Today!;
        var total = (day.End - day.Start).TotalMinutes;
        return Left + Math.Clamp((t - day.Start).TotalMinutes / total, 0, 1) * Width0;
    }

    /// <summary>The real elevation, scaled to today's highest and lowest point: a smooth curve with the horizon in it.</summary>
    double Y(double elevation)
    {
        var day = plan!.Today!;
        var high = Math.Max(day.Highest, 5) + 2;
        var low = Math.Min(day.Lowest, -5) - 2;
        return Top + (high - elevation) / (high - low) * CurveHeight;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (plan?.Today is not { } day) return;
        var text2 = Theme.Brush("Text3");
        var text4 = Theme.Brush("Text4");
        var line = Theme.Brush("Line");
        var accent = Theme.Brush("Accent");
        var surface2 = Theme.Brush("Surface2");
        var culture = Strings.Culture;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Daylight behind the curve: above the horizon a little lighter.
        var horizonY = Y(0);
        dc.DrawRectangle(surface2, null, new Rect(Left, Top, Width0, horizonY - Top));

        // Hour lines and labels.
        var linePen = new Pen(line, 1);
        for (var h = 0; h <= 24; h += 3)
        {
            var t = day.Date.ToDateTime(TimeOnly.MinValue).AddHours(h);
            var x = h == 24 ? Left + Width0 : X(t);
            dc.DrawLine(linePen, new Point(x, Top), new Point(x, Top + CurveHeight));
            var label = new FormattedText(h.ToString("00", culture) + ":00", culture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, text4, dpi);
            dc.DrawText(label, new Point(Math.Min(x - label.Width / 2, ActualWidth - label.Width - 2), ActualHeight - AxisHeight + 4));
        }

        // Horizon and the band where the real elevation counts.
        dc.DrawLine(new Pen(text4, 1) { DashStyle = DashStyles.Dash }, new Point(Left, horizonY), new Point(Left + Width0, horizonY));
        var horizonLabel = new FormattedText("0°", culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, text4, dpi);
        dc.DrawText(horizonLabel, new Point(Left - horizonLabel.Width - 8, horizonY - horizonLabel.Height / 2));
        var topLabel = new FormattedText(Strings.Degrees(day.Highest), culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, text4, dpi);
        dc.DrawText(topLabel, new Point(Left - topLabel.Width - 8, Y(day.Highest) - topLabel.Height / 2));

        // The sun's arc.
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var i = 0; i < day.Samples; i += 5)
            {
                var p = new Point(X(day.TimeAt(i)), Y(day.ElevationAt(i)));
                if (i == 0) g.BeginFigure(p, false, false);
                else g.LineTo(p, true, true);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(Theme.Brush("AccentGradient"), 3) { LineJoin = PenLineJoin.Round }, geometry);

        // Light and dark.
        var modeTop = Top + CurveHeight + 10;
        if (plan.TodayModes.Count > 0 || plan.Dark != null)
        {
            var darkBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x4F, 0x86));
            var lightBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xE6, 0xC4));
            var events = plan.TodayModes.OrderBy(e => e.At).ToList();
            // State at the start of the day: the opposite of the first change, or the plan's state if none.
            var state = events.Count > 0 ? !events[0].Dark : plan.Dark ?? false;
            var from = day.Start;
            foreach (var (at, dark) in events.Append((day.End, !state)))
            {
                var r = new Rect(X(from), modeTop, Math.Max(0, X(at) - X(from)), ModeHeight);
                dc.DrawRoundedRectangle(state ? darkBrush : lightBrush, null, r, 2, 2);
                from = at;
                state = dark;
            }
        }

        // The images: the desktop's, and below them the lock screen's own when it has a package of its own.
        var stripTop = modeTop + ModeHeight + 8;
        if (plan.TodayModes.Count > 0 || plan.Dark != null) Mark(dc, Strings.T("Color mode", "Farbmodus"), modeTop, ModeHeight, text2, dpi);
        Mark(dc, Strings.T("Desktop", "Desktop"), stripTop, StripHeight, text2, dpi);
        var bottom = stripTop + StripHeight;
        if (package != null && plan.TodaySlots.Count > 0) DrawStrip(dc, day, package, plan.TodaySlots, stripTop, dpi);
        else
        {
            var hint = new FormattedText(Strings.T("No image package chosen yet", "Noch kein Bildpaket gewählt"), culture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, text2, dpi);
            dc.DrawRoundedRectangle(surface2, null, new Rect(Left, stripTop, Width0, StripHeight), 6, 6);
            dc.DrawText(hint, new Point(Left + (Width0 - hint.Width) / 2, stripTop + (StripHeight - hint.Height) / 2));
        }
        if (TwoStrips)
        {
            var lockTop = stripTop + StripHeight + StripGap;
            DrawStrip(dc, day, plan.LockPackage!, plan.LockTodaySlots, lockTop, dpi);
            // Which strip is which: the symbols of the two entries below, Desktop and Lock screen.
            Mark(dc, Strings.T("Lock screen", "Sperrbildschirm"), lockTop, StripHeight, text2, dpi);
            bottom = lockTop + StripHeight;
        }

        // Now.
        if (showNow && plan.At >= day.Start && plan.At <= day.End)
        {
            var x = X(plan.At);
            var pen = new Pen(accent, 2);
            dc.DrawLine(pen, new Point(x, Top - 4), new Point(x, bottom + 2));
            var sunY = Y(day.ElevationAt(plan.At));
            dc.DrawEllipse(Theme.Brush("Bg"), new Pen(accent, 3), new Point(x, sunY), 7, 7);
        }
    }

    /// <summary>One package's images over the day, each from its start to the next one's.</summary>
    void DrawStrip(DrawingContext dc, SunDay day, Package pkg, IReadOnlyList<Slot> slots, double top, double dpi)
    {
        var surface2 = Theme.Brush("Surface2");
        // Before the first image of the day the last one of the night before is still on.
        var segments = new List<(DateTime From, DateTime To, Slot Slot)> { (day.Start, slots[0].Start, slots[^1]) };
        for (var i = 0; i < slots.Count; i++)
            segments.Add((slots[i].Start, i + 1 < slots.Count ? slots[i + 1].Start : day.End, slots[i]));

        foreach (var (from, to, slot) in segments)
        {
            var x0 = X(from);
            var x1 = X(to);
            if (x1 - x0 < 1) continue;
            var r = new Rect(x0 + 1, top, x1 - x0 - 2, StripHeight);
            var thumb = Thumb(pkg.PathOf(pkg.Images[slot.Image]));
            if (thumb != null)
            {
                var brush = new ImageBrush(thumb) { Stretch = Stretch.UniformToFill };
                brush.Freeze();
                dc.DrawRoundedRectangle(brush, null, r, 6, 6);
            }
            else dc.DrawRoundedRectangle(surface2, null, r, 6, 6);

            if (r.Width > 44 && from > day.Start)
            {
                var label = new FormattedText(Strings.Time(from) + (slot.Mirrored ? " \u21BA" : ""), Strings.Culture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 11, Brushes.White, dpi);
                var pill = new Rect(r.X + 4, r.Y + 4, label.Width + 8, label.Height + 2);
                // The now line runs on top: a time under it would be cut in two. The time moves to the right of the
                // line, or stays away when the picture has no room for it there (the tooltip still says it).
                if (NowX(day) is { } nowX && nowX >= pill.Left - 2 && nowX <= pill.Right + 2)
                {
                    if (nowX + 4 + pill.Width > r.Right - 4) continue;
                    pill.X = nowX + 4;
                }
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xA0, 0, 0, 0)), null, pill, 4, 4);
                dc.DrawText(label, new Point(pill.X + 4, pill.Y + 1));
            }
        }
    }

    /// <summary>Where the now line runs, when it shows.</summary>
    double? NowX(SunDay day) => showNow && plan != null && plan.At >= day.Start && plan.At <= day.End ? X(plan.At) : null;

    /// <summary>A name in the left column, beside a band or strip.</summary>
    void Mark(DrawingContext dc, string name, double top, double height, Brush brush, double dpi)
    {
        var label = new FormattedText(name, Strings.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, brush, dpi);
        dc.DrawText(label, new Point(0, top + (height - label.Height) / 2));
    }

    /// <summary>From the shared cache, read off the window's thread; the day is drawn again when a picture arrives.</summary>
    BitmapSource? Thumb(string path) => ThumbCache.Get(path, 320, InvalidateVisual);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (plan?.Today is not { } day) return;
        var x = e.GetPosition(this).X;
        var f = Math.Clamp((x - Left) / Width0, 0, 1);
        var t = day.Start.AddMinutes(f * (day.End - day.Start).TotalMinutes);
        var elevation = day.ElevationAt(t);
        // One fact per line.
        var text = $"{Strings.Time(t)}\n{Strings.T("Sun at", "Sonne bei")} {Strings.Degrees(Math.Round(elevation, 1))}";
        if (package != null && plan.TodaySlots.Count > 0)
        {
            var slot = plan.TodaySlots.LastOrDefault(s => s.Start <= t) ?? plan.TodaySlots[^1];
            text += $"\n{(TwoStrips ? Strings.T("Desktop", "Desktop") : Strings.T("Image", "Bild"))}: {package.Images[slot.Image].DisplayName}";
        }
        if (TwoStrips)
        {
            var slot = plan.LockTodaySlots.LastOrDefault(s => s.Start <= t) ?? plan.LockTodaySlots[^1];
            text += $"\n{Strings.T("Lock screen", "Sperrbildschirm")}: {plan.LockPackage!.Images[slot.Image].DisplayName}";
        }
        ToolTip = text;
    }
}
