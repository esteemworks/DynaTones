using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>
/// The Color mode tab: on top "Light and dark with the day" with the way it switches, the day as a band from 0 to 24
/// with handles at the two switches, and the two rules as sentences; below "What changes along" as three cards, Apps,
/// Taskbar and Start, Mouse pointer, each with a small picture of day and night right above its choice, so every
/// setting shows at once what it does.
/// </summary>
public partial class MainWindow
{
    bool modeFollowsTheme;

    void BuildModePage()
    {
        // The tinted fields and pictures are drawn for light or dark: drawn again when the window's mode changes.
        if (!modeFollowsTheme)
        {
            modeFollowsTheme = true;
            void Again() => Dispatcher.BeginInvoke(BuildModePage);
            Theme.Changed += Again;
            Closed += (_, _) => Theme.Changed -= Again;
        }
        ModePanel.Children.Clear();
        if (ProfileNote() is { } note) ModePanel.Children.Add(note);
        var a = Engine.Settings.Appearance;

        // ---------- Light and dark with the day ----------
        var top = new StackPanel();
        ModePanel.Children.Add(new Border { Style = S("Card"), Child = top, Padding = new Thickness(24, 16, 24, 18), Margin = new Thickness(0, 0, 0, 16) });
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = Ui.Horizontal(new TextBlock { Style = S("CardTitle"), Text = Strings.T("Light and dark with the day", "Hell und dunkel mit dem Tag"), VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Style = S("InfoGlyph") });
        title.ToolTip = Strings.T("The same setting as Windows Settings > Personalization > Colors. Works without admin rights.",
            "Dieselbe Einstellung wie in den Windows-Einstellungen unter Personalisierung > Farben. Geht ohne Adminrechte.");
        title.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(title);
        if (a.Enabled)
        {
            var trigger = Ui.Segments(new[]
            {
                (AppearanceTrigger.Sun, Strings.T("By the sun", "Nach der Sonne")),
                (AppearanceTrigger.Times, Strings.T("At set times", "Zu Uhrzeiten")),
                (AppearanceTrigger.Image, Strings.T("Like the picture", "Wie das Bild")),
            }, a.Trigger, t => ChangeAndRebuild(c => c.Appearance.Trigger = t, BuildModePage), [
                Strings.T("Light from sunrise to sunset at your location, each with an offset", "Hell von Sonnenauf- bis -untergang an deinem Standort, jeweils mit Versatz"),
                Strings.T("At two fixed clock times every day", "Zu zwei festen Uhrzeiten an jedem Tag"),
                Strings.T("Dark while the picture shown belongs below the horizon: always goes with the background", "Dunkel, solange das gezeigte Bild unter den Horizont gehört: passt immer zum Hintergrund"),
            ]);
            trigger.Margin = new Thickness(16, 0, 16, 0);
            Grid.SetColumn(trigger, 1);
            head.Children.Add(trigger);
        }
        var toggle = Locked(Ui.Toggle(a.Enabled, on => ChangeAndRebuild(c => c.Appearance.Enabled = on, BuildModePage)), Policies.ColorModeEnabled)
            .Tip(Strings.T("Off: DynaTones leaves the color mode to you and only changes the background.", "Aus: DynaTones überlässt den Farbmodus dir und wechselt nur den Hintergrund."));
        toggle.VerticalAlignment = VerticalAlignment.Center;
        Speak(toggle, Strings.T("Switch the color mode with the day", "Farbmodus mit dem Tag umschalten"));
        Grid.SetColumn(toggle, 2);
        head.Children.Add(toggle);
        top.Children.Add(head);

        if (!a.Enabled)
        {
            top.Children.Add(new TextBlock { Style = S("Muted"), Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap,
                Text = Strings.T("DynaTones leaves the color mode to you and only changes the background.", "DynaTones überlässt den Farbmodus dir und wechselt nur den Hintergrund.") });
        }
        else
        {
            var band = new ModeBand { Margin = new Thickness(0, 14, 0, 12) };
            band.Show(Engine.Plan, a, (light, dark) => ChangeAndRebuild(c =>
            {
                if (light is { } l) { if (c.Appearance.Trigger == AppearanceTrigger.Sun) c.Appearance.SunriseOffsetMinutes = l; else c.Appearance.LightFrom = Minutes(l); }
                if (dark is { } d) { if (c.Appearance.Trigger == AppearanceTrigger.Sun) c.Appearance.SunsetOffsetMinutes = d; else c.Appearance.DarkFrom = Minutes(d); }
            }, BuildModePage));
            top.Children.Add(band);

            if (a.Trigger != AppearanceTrigger.Image)
            {
                var rules = new UniformGrid { Columns = 2 };
                rules.Children.Add(Rule(true, a));
                rules.Children.Add(Rule(false, a));
                top.Children.Add(rules);
            }
            else
                top.Children.Add(new TextBlock { Style = S("Muted"), TextWrapping = TextWrapping.Wrap,
                    Text = Strings.T("Dark while the picture on the desktop belongs below the horizon, light while it belongs above it: the color mode always goes with the background.",
                        "Dunkel, solange das Bild auf dem Desktop unter den Horizont gehört, hell, solange es darüber gehört: Der Farbmodus passt immer zum Hintergrund.") });
        }

        // ---------- What changes along ----------
        ModePanel.Children.Add(new TextBlock { Style = S("SectionLabel"), Text = Strings.T("WHAT CHANGES ALONG", "WAS MITWECHSELT"), Margin = new Thickness(0, 4, 0, 8) });
        var cards = new UniformGrid { Columns = 3, Margin = new Thickness(-6, 0, -6, 0) };
        ModePanel.Children.Add(cards);
        var targets = new[]
        {
            (ModeTarget.Switch, Strings.T("Change with the day", "Mit dem Tag wechseln")),
            (ModeTarget.AlwaysLight, Strings.T("Always light", "Immer hell")),
            (ModeTarget.AlwaysDark, Strings.T("Always dark", "Immer dunkel")),
            (ModeTarget.Leave, Strings.T("Leave alone", "Nicht anfassen")),
        };
        string?[] targetTips =
        [
            Strings.T("Light by day, dark by night, as set above", "Tagsüber hell, nachts dunkel, wie oben eingestellt"), null, null,
            Strings.T("DynaTones does not touch it; you set it in Windows", "DynaTones ändert hier nichts; du stellst es in Windows ein"),
        ];
        bool Light(ModeTarget t, bool day, bool? now) => t switch
        {
            ModeTarget.AlwaysLight => true,
            ModeTarget.AlwaysDark => false,
            ModeTarget.Leave => now ?? true,
            _ => day,
        };

        var apps = ChangeCard(cards, Strings.T("Apps", "Apps"),
            Strings.T("Explorer, Settings and every app that follows the Windows color mode.", "Explorer, Einstellungen und alle Apps, die dem Farbmodus von Windows folgen."));
        apps.Children.Add(Pair(AppWindow(Light(a.Apps, true, Appearance.AppsLight), true), AppWindow(Light(a.Apps, false, Appearance.AppsLight), false)));
        apps.Children.Add(Fill(Locked(Ui.Choice(targets, a.Apps, t => ChangeAndRebuild(c => c.Appearance.Apps = t, BuildModePage), 200, targetTips), Policies.AppsColorMode)));

        var system = ChangeCard(cards, Strings.T("Taskbar and Start", "Taskleiste und Start"),
            Strings.T("Taskbar, Start, notifications. Some like Windows dark with light apps.", "Taskleiste, Start, Benachrichtigungen. Manche mögen Windows dunkel mit hellen Apps."));
        system.Children.Add(Pair(Taskbar(Light(a.System, true, Appearance.SystemLight), true), Taskbar(Light(a.System, false, Appearance.SystemLight), false)));
        system.Children.Add(Fill(Locked(Ui.Choice(targets, a.System, t => ChangeAndRebuild(c => c.Appearance.System = t, BuildModePage), 200, targetTips), Policies.WindowsColorMode)));

        var pointer = ChangeCard(cards, Strings.T("Mouse pointer", "Mauszeiger"),
            Strings.T("A pointer scheme for the day and one for the night, from the schemes in Windows and the pointer packages you add.",
                "Ein Zeigerschema für den Tag und eines für die Nacht, aus den Schemata von Windows und den Zeigerpaketen, die du hinzufügst."),
            Ui.Button(Strings.T("Add pointer package", "Zeigerpaket hinzufügen"), (_, _) => AddPointerPackage(), "GhostButton")
                .Tip(Strings.T("A folder or a .zip with an install.inf or .cur and .ani files. Stays with your user, no admin rights.",
                    "Ein Ordner oder eine .zip mit install.inf oder .cur- und .ani-Dateien. Bleibt bei deinem Benutzer, ohne Adminrechte.")));
        pointer.Children.Add(Pair(PointerPreview(a.CursorLight, true), PointerPreview(a.CursorDark, false)));
        var schemes = new List<(string?, string)> { (null, Strings.T("Leave alone", "Nicht anfassen")) };
        schemes.AddRange(Appearance.CursorSchemes().Select(s => ((string?)s, s)));
        var choices = new UniformGrid { Columns = 2 };
        var day = Fill(Ui.Choice(schemes, a.CursorLight, v => ChangeAndRebuild(c => c.Appearance.CursorLight = v, BuildModePage), 120));
        day.Margin = new Thickness(0, 0, 4, 0);
        Speak(day, Strings.T("Pointer by day", "Mauszeiger am Tag"));
        var night = Fill(Ui.Choice(schemes, a.CursorDark, v => ChangeAndRebuild(c => c.Appearance.CursorDark = v, BuildModePage), 120));
        night.Margin = new Thickness(4, 0, 0, 0);
        Speak(night, Strings.T("Pointer by night", "Mauszeiger in der Nacht"));
        choices.Children.Add(day);
        choices.Children.Add(night);
        pointer.Children.Add(choices);
    }

    static string Minutes(int minutes) => new TimeOnly(((minutes % 1440) + 1440) % 1440 / 60, ((minutes % 1440) + 1440) % 1440 % 60).ToString("HH:mm", CultureInfo.InvariantCulture);

    static T Fill<T>(T element) where T : FrameworkElement
    {
        element.Width = double.NaN;
        element.HorizontalAlignment = HorizontalAlignment.Stretch;
        return element;
    }

    /// <summary>One rule as a sentence in a field tinted like what it starts: "Light from [30] min. [after] sunrise".</summary>
    UIElement Rule(bool light, AppearanceSettings a)
    {
        var dark = Theme.IsDark;
        var tint = light ? (dark ? Color.FromRgb(0x3A, 0x33, 0x24) : Color.FromRgb(0xFB, 0xF7, 0xEC)) : (dark ? Color.FromRgb(0x2A, 0x2C, 0x42) : Color.FromRgb(0xEE, 0xEF, 0xF6));
        var ink = light ? (dark ? Color.FromRgb(0xE8, 0xC0, 0x5A) : Color.FromRgb(0xB8, 0x86, 0x0B)) : (dark ? Color.FromRgb(0xA9, 0xAE, 0xE8) : Color.FromRgb(0x3F, 0x43, 0x78));
        var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        TextBlock Word(string text) => new() { Style = S("RowLabel"), Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        row.Children.Add(new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = light ? "" : "", FontSize = 15, Foreground = new SolidColorBrush(ink), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        row.Children.Add(Word(light ? Strings.T("Light from", "Hell ab") : Strings.T("Dark from", "Dunkel ab")));
        if (a.Trigger == AppearanceTrigger.Sun)
        {
            var value = light ? a.SunriseOffsetMinutes : a.SunsetOffsetMinutes;
            var minutes = Math.Abs(value);
            var after = value >= 0;
            void Set() => Change(c =>
            {
                if (light) c.Appearance.SunriseOffsetMinutes = after ? minutes : -minutes;
                else c.Appearance.SunsetOffsetMinutes = after ? minutes : -minutes;
            });
            var field = Ui.Field(minutes.ToString(CultureInfo.InvariantCulture), 48, text =>
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v is < 0 or > 240) return false;
                minutes = v;
                Set();
                Dispatcher.BeginInvoke(BuildModePage);
                return true;
            }).Tip(Strings.T("0 to 240 minutes. Enter confirms.", "0 bis 240 Minuten. Enter übernimmt."));
            field.Margin = new Thickness(0, 0, 8, 0);
            Speak(field, Strings.T("Minutes", "Minuten"));
            row.Children.Add(field);
            row.Children.Add(Word(Strings.T("min.", "Min.")));
            var direction = Ui.Choice(new[] { (true, Strings.T("after", "nach")), (false, Strings.T("before", "vor")) }, after, v =>
            {
                after = v;
                Set();
                Dispatcher.BeginInvoke(BuildModePage);
            }, 96);
            direction.Margin = new Thickness(0, 0, 8, 0);
            Speak(direction, Strings.T("after or before", "nach oder vor"));
            row.Children.Add(direction);
            row.Children.Add(Word(light ? Strings.T("sunrise", "Sonnenaufgang") : Strings.T("sunset", "Sonnenuntergang")));
        }
        else
        {
            var field = TimeField(light ? a.LightFrom : a.DarkFrom, v =>
            {
                Change(c =>
                {
                    if (light) c.Appearance.LightFrom = v;
                    else c.Appearance.DarkFrom = v;
                });
                Dispatcher.BeginInvoke(BuildModePage);
            });
            field.Margin = new Thickness(0, 0, 8, 0);
            Speak(field, light ? Strings.T("Light from", "Hell ab") : Strings.T("Dark from", "Dunkel ab"));
            row.Children.Add(field);
            row.Children.Add(Word(Strings.T("o'clock", "Uhr")));
        }
        return new Border
        {
            Background = new SolidColorBrush(tint),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8, 14, 8),
            MinHeight = 48,
            Margin = light ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0),
            Child = row,
        };
    }

    /// <summary>One card under "What changes along": title (with its own action on the right), then pictures, then the choice.</summary>
    StackPanel ChangeCard(Panel parent, string title, string help, FrameworkElement? action = null)
    {
        var stack = new StackPanel();
        var head = new Grid { Margin = new Thickness(0, 0, 0, 10), MinHeight = 28 };
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = Ui.Horizontal(new TextBlock { Style = S("CardTitle"), Text = title, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Style = S("InfoGlyph") });
        name.ToolTip = help;
        name.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(name);
        if (action != null)
        {
            action.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(action, 1);
            head.Children.Add(action);
        }
        stack.Children.Add(head);
        parent.Children.Add(new Border { Style = S("Card"), Child = stack, Padding = new Thickness(16, 14, 16, 16), Margin = new Thickness(6, 0, 6, 12) });
        return stack;
    }

    /// <summary>Two pictures side by side, labelled Day and Night.</summary>
    UIElement Pair(UIElement day, UIElement night)
    {
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 10) };
        foreach (var element in new[] { day, night })
            if (element is Border b)
            {
                b.BorderBrush = B("Line");
                b.BorderThickness = new Thickness(1);
            }
        foreach (var (picture, label, margin) in new[] { (day, Strings.T("Day", "Tag"), new Thickness(0, 0, 4, 0)), (night, Strings.T("Night", "Nacht"), new Thickness(4, 0, 0, 0)) })
        {
            var stack = new StackPanel { Margin = margin };
            stack.Children.Add(picture);
            stack.Children.Add(new TextBlock { Style = S("Muted"), Text = label, Margin = new Thickness(0, 4, 0, 0) });
            grid.Children.Add(stack);
        }
        return grid;
    }

    static Brush Hex(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    /// <summary>A desktop with an app window, light or dark.</summary>
    static UIElement AppWindow(bool light, bool day)
    {
        var lines = new StackPanel();
        lines.Children.Add(new Border { Height = 10, Background = Hex(light ? "#F3F3F3" : "#202020"), CornerRadius = new CornerRadius(4, 4, 0, 0) });
        lines.Children.Add(new Border { Height = 6, Width = 60, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 8, 0, 0), CornerRadius = new CornerRadius(3), Background = Hex(light ? "#D6D6D6" : "#5A5A5A") });
        lines.Children.Add(new Border { Height = 6, Width = 40, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 6, 0, 0), CornerRadius = new CornerRadius(3), Background = Hex(light ? "#E5E5E5" : "#444444") });
        var window = new Border { CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = Hex(light ? "#D6D6D6" : "#3A3A3A"), Background = Hex(light ? "#FFFFFF" : "#2B2B2B"), Child = lines };
        return new Border { Height = 78, CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Background = Hex(day ? "#DDE7F2" : "#2A2D4A"), Child = window };
    }

    /// <summary>A desktop with the taskbar, light or dark.</summary>
    static UIElement Taskbar(bool light, bool day)
    {
        var icons = Ui.Horizontal();
        icons.HorizontalAlignment = HorizontalAlignment.Center;
        icons.VerticalAlignment = VerticalAlignment.Center;
        foreach (var color in new[] { "#4DABF7", light ? "#6B6B6B" : "#8A8A8A", light ? "#6B6B6B" : "#8A8A8A" })
            icons.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2), Background = Hex(color), Margin = new Thickness(2.5, 0, 2.5, 0) });
        var bar = new Border { Height = 16, VerticalAlignment = VerticalAlignment.Bottom, Background = Hex(light ? "#EDEDED" : "#202020"), Child = icons };
        return new Border { Height = 78, CornerRadius = new CornerRadius(6), ClipToBounds = true, Background = Hex(day ? "#DDE7F2" : "#2A2D4A"), Child = bar };
    }

    /// <summary>The normal pointer and the text pointer of a scheme, from its own files; Windows' when it has none.</summary>
    UIElement PointerPreview(string? scheme, bool day)
    {
        var files = PointerPackages.Files(scheme);
        var row = Ui.Horizontal();
        row.HorizontalAlignment = HorizontalAlignment.Center;
        row.VerticalAlignment = VerticalAlignment.Center;
        foreach (var role in new[] { 0, 5 })
        {
            var picture = PointerPackages.Picture(role < files.Count ? files[role] : null);
            UIElement shown = picture != null
                ? new Image { Source = picture, Width = 32, Height = 32, Stretch = Stretch.None }
                : DrawnPointer(role == 0, day);
            if (shown is FrameworkElement f) f.Margin = new Thickness(6, 0, 6, 0);
            row.Children.Add(shown);
        }
        return new Border { Height = 78, CornerRadius = new CornerRadius(6), Background = Hex(day ? "#F3F3F3" : "#2B2B2B"), Child = row };
    }

    /// <summary>Windows' own arrow or text pointer, drawn: for a role a scheme leaves to Windows.</summary>
    static UIElement DrawnPointer(bool arrow, bool day)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(arrow ? "M1,1 L1,16 L5,12 L8,19 L10,18 L7,11 L12,11 Z" : "M3,2 L9,2 M6,2 L6,18 M3,18 L9,18"),
            Fill = arrow ? Brushes.White : null,
            Stroke = Brushes.Black,
            StrokeThickness = arrow ? 1.1 : 1.6,
            Width = 16,
            Height = 22,
            Stretch = Stretch.None,
        };
        return path;
    }

    async void AddPointerPackage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.T("Add a pointer package", "Zeigerpaket hinzufügen"),
            Filter = Strings.T("Pointer package (*.zip;*.inf)|*.zip;*.inf", "Zeigerpaket (*.zip;*.inf)|*.zip;*.inf"),
        };
        if (dialog.ShowDialog(this) != true) return;
        var source = dialog.FileName.EndsWith(".inf", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(dialog.FileName)! : dialog.FileName;
        try
        {
            var name = await Task.Run(() => PointerPackages.Import(source));
            BuildModePage();
            _ = this.dialog.ShowMessageAsync(Strings.T("Pointer package added", "Zeigerpaket hinzugefügt"), Strings.T($"\"{name}\" can now be chosen for day or night.", $"„{name}“ ist jetzt für Tag oder Nacht wählbar."));
        }
        catch (Exception ex)
        {
            Log.Warning($"Pointer package could not be added: {ex.Message}");
            _ = this.dialog.ShowMessageAsync(Strings.T("The pointer package could not be added", "Das Zeigerpaket konnte nicht hinzugefügt werden"), ex.Message, problem: true);
        }
    }
}

/// <summary>
/// The day from 0 to 24 as a band of light and dark, with sunrise and sunset marked and a handle at each switch. For
/// "By the sun" and "At set times" the handles can be dragged; the new time is taken when the mouse is let go.
/// </summary>
sealed class ModeBand : FrameworkElement
{
    Plan? plan;
    AppearanceSettings? settings;
    Action<int?, int?>? commit;
    DateTime? sunrise, sunset;
    DateTime? lightAt, darkAt;
    int dragging; // 0 none, 1 light, 2 dark
    double dragX;

    const double BandTop = 18, BandHeight = 28, AxisTop = 52;

    public ModeBand()
    {
        Height = 68;
        Theme.Changed += InvalidateVisual;
        Cursor = Cursors.Arrow;
    }

    public void Show(Plan? value, AppearanceSettings a, Action<int?, int?> changed)
    {
        plan = value;
        settings = a;
        commit = changed;
        sunrise = sunset = lightAt = darkAt = null;
        if (plan?.Today is { } day)
        {
            sunrise = day.Crossing(Sun.SunriseElevation, true);
            sunset = day.Crossing(Sun.SunriseElevation, false);
            foreach (var (at, dark) in plan.TodayModes)
            {
                if (!dark && lightAt == null) lightAt = at;
                if (dark && at > (lightAt ?? DateTime.MinValue)) darkAt ??= at;
            }
        }
        var draggable = a.Trigger != AppearanceTrigger.Image;
        Cursor = draggable ? Cursors.SizeWE : Cursors.Arrow;
        ToolTip = draggable
            ? Strings.T("Drag a handle to move the switch; it is taken when you let go.", "Einen Griff ziehen, um den Wechsel zu verschieben; er gilt beim Loslassen.")
            : Strings.T("Light and dark as the pictures of the day go.", "Hell und dunkel, wie die Bilder des Tages gehen.");
        InvalidateVisual();
    }

    DateTime Start => plan!.Today!.Start;
    double X(DateTime t) => Math.Clamp((t - Start).TotalMinutes / 1440, 0, 1) * ActualWidth;
    DateTime At(double x) => Start.AddMinutes(Math.Round(Math.Clamp(x / Math.Max(1, ActualWidth), 0, 1) * 1440 / 5) * 5);

    protected override void OnRender(DrawingContext dc)
    {
        if (plan?.Today is not { } day || ActualWidth <= 0) return;
        var culture = Strings.Culture;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = Theme.Brush("Text4");
        var light = new SolidColorBrush(Color.FromRgb(0xF3, 0xE3, 0xC3));
        var darkBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x43, 0x78));
        FormattedText Label(string s, double size, Brush brush, bool bold = false) =>
            new(s, culture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, brush, dpi);

        // The band, from the plan; while a handle is dragged, with the handle's new place.
        var l = dragging == 1 ? At(dragX) : lightAt;
        var d = dragging == 2 ? At(dragX) : darkAt;
        dc.PushClip(new RectangleGeometry(new Rect(0, BandTop, ActualWidth, BandHeight), 6, 6));
        dc.DrawRectangle(darkBrush, null, new Rect(0, BandTop, ActualWidth, BandHeight));
        if (l is { } lf && d is { } df && df > lf) dc.DrawRectangle(light, null, new Rect(X(lf), BandTop, X(df) - X(lf), BandHeight));
        else if (plan.Dark == false && l == null && d == null) dc.DrawRectangle(light, null, new Rect(0, BandTop, ActualWidth, BandHeight));
        dc.Pop();

        // Sunrise and sunset.
        var line = new Pen(Theme.Brush("Text4"), 1);
        foreach (var (at, name) in new[] { (sunrise, Strings.T("Sunrise", "Aufgang")), (sunset, Strings.T("Sunset", "Untergang")) })
        {
            if (at is not { } t) continue;
            var x = X(t);
            dc.DrawLine(line, new Point(x, BandTop - 4), new Point(x, BandTop + BandHeight + 4));
            var label = Label($"{name} {Strings.Time(t)}", 11, text);
            dc.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, 0, ActualWidth - label.Width), 0));
        }

        // The light span, named in its middle.
        if (l is { } a && d is { } b && b > a)
        {
            var span = Label(Strings.T($"light, {Strings.Time(a)} to {Strings.Time(b)}", $"hell, {Strings.Time(a)} bis {Strings.Time(b)}"), 12, new SolidColorBrush(Color.FromRgb(0x6B, 0x4E, 0x16)), true);
            if (X(b) - X(a) > span.Width + 24) dc.DrawText(span, new Point((X(a) + X(b) - span.Width) / 2, BandTop + (BandHeight - span.Height) / 2));
        }

        // Handles.
        if (settings?.Trigger != AppearanceTrigger.Image)
        {
            var accent = new Pen(Theme.Brush("Accent"), 2);
            foreach (var at in new[] { l, d })
            {
                if (at is not { } t) continue;
                var x = X(t);
                dc.DrawRoundedRectangle(Theme.Brush("Surface"), accent, new Rect(x - 6, BandTop - 4, 12, BandHeight + 8), 6, 6);
            }
        }

        // Hours.
        foreach (var h in new[] { 0, 6, 12, 18, 24 })
        {
            var label = Label($"{h:00}:00", 11, text);
            var x = h * ActualWidth / 24;
            dc.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, 0, ActualWidth - label.Width), AxisTop));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (settings?.Trigger == AppearanceTrigger.Image || plan?.Today == null) return;
        var x = e.GetPosition(this).X;
        var toLight = lightAt is { } a ? Math.Abs(X(a) - x) : double.MaxValue;
        var toDark = darkAt is { } b ? Math.Abs(X(b) - x) : double.MaxValue;
        if (Math.Min(toLight, toDark) > 24) return;
        dragging = toLight <= toDark ? 1 : 2;
        dragX = x;
        CaptureMouse();
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging == 0) return;
        dragX = e.GetPosition(this).X;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (dragging == 0) return;
        ReleaseMouseCapture();
        var at = At(dragX);
        var which = dragging;
        dragging = 0;
        if (settings == null || commit == null) return;
        if (settings.Trigger == AppearanceTrigger.Sun)
        {
            // As an offset from sunrise or sunset, within the four hours the field allows.
            var anchor = which == 1 ? sunrise : sunset;
            if (anchor is not { } s) return;
            var offset = (int)Math.Clamp(Math.Round((at - s).TotalMinutes / 5) * 5, -240, 240);
            if (which == 1) commit(offset, null);
            else commit(null, offset);
        }
        else
        {
            var minutes = (int)(at - Start).TotalMinutes;
            if (which == 1) commit(minutes, null);
            else commit(null, minutes);
        }
    }
}
