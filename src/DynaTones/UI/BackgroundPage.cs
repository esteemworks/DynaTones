using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>The pages of the Background tab: the overview first, the others reached from it.</summary>
enum BgPage { Overview, Desktop, Lock, Package, Image }

/// <summary>
/// The Background tab: on top what shows now on the desktop and the lock screen with the day below it, then the way
/// into the settings of each, then the packages. Built in code; the overview is built again only when what it shows
/// changed, so a resolve every minute does not redraw it.
/// </summary>
public partial class MainWindow
{
    BgPage page = BgPage.Overview;
    string? pagePackageId;
    int pageImage;
    BgPage imageBack = BgPage.Desktop;
    DayTimeline? timeline;
    string? shownKey;

    Style S(string key) => (Style)FindResource(key);
    Brush B(string key) => (Brush)FindResource(key);

    // ---------- Navigation ----------

    void OpenPage(BgPage to, string? packageId = null, int image = 0)
    {
        if (to == BgPage.Image) imageBack = page == BgPage.Image ? imageBack : page;
        page = to;
        if (packageId != null) pagePackageId = packageId;
        pageImage = image;
        BuildBackground();
        BackgroundView?.ScrollToHome();
    }

    void OpenPackage(string id)
    {
        TabBackground.IsChecked = true;
        OpenPage(BgPage.Package, id);
    }

    /// <summary>For the snapshot: each page of the tab in turn.</summary>
    internal void ShowBackgroundPage(string name)
    {
        TabBackground.IsChecked = true;
        galleryOpen = false;
        ShowPage();
        var first = Engine.Package ?? Packages.LoadAll().FirstOrDefault();
        switch (name)
        {
            case "desktop": OpenPage(BgPage.Desktop); break;
            case "lock": OpenPage(BgPage.Lock); break;
            case "package" when first != null: OpenPage(BgPage.Package, first.Id); break;
            case "image" when first != null:
                page = BgPage.Desktop;
                OpenPage(BgPage.Image, first.Id, Math.Min(1, first.Images.Count - 1));
                break;
            default: OpenPage(BgPage.Overview); break;
        }
    }

    internal static readonly string[] BackgroundPages = ["desktop", "lock", "package", "image"];

    void BuildBackground()
    {
        if (BackgroundPanel == null) return;
        // A page has to come at once; one that takes long is worth a line, so a slow spot is found again.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        shownKey = null;
        timeline = null;
        BackgroundPanel.Children.Clear();
        switch (page)
        {
            case BgPage.Desktop: BuildDesktopPage(); break;
            case BgPage.Lock: BuildLockPage(); break;
            case BgPage.Package when FindPackage(pagePackageId) is { } p: BuildPackagePage(p); break;
            case BgPage.Image when FindPackage(pagePackageId) is { } p && pageImage < p.Images.Count: BuildImagePage(p, pageImage); break;
            default:
                page = BgPage.Overview;
                BuildOverview();
                break;
        }
        shownKey = PageKey();
        if (watch.ElapsedMilliseconds > 150) Log.Info($"Page {page} took {watch.ElapsedMilliseconds} ms to build");
    }

    static Package? FindPackage(string? id) => id == null ? null : Packages.Load(id);

    /// <summary>After every resolve: the header always, a page only when what it shows has changed.</summary>
    void RefreshBackground()
    {
        var paused = Engine.Settings.PausedUntil is { } until && until > DateTime.Now;
        PauseText.Text = paused ? Strings.T("Resume", "Fortsetzen") : Strings.T("Pause", "Pausieren");
        PauseIcon.Text = paused ? "" : "";
        PauseChevron.Visibility = paused ? Visibility.Collapsed : Visibility.Visible;
        Speak(PauseButton, PauseText.Text);
        SummaryText.Text = Summary(paused);

        if (page is BgPage.Overview or BgPage.Desktop or BgPage.Lock && PageKey() != shownKey) BuildBackground();
        else if (timeline != null) timeline.Show(PreviewPlan(), Engine.Package, previewDate == null);
    }

    /// <summary>What the dynamic pages show, as text: when it differs from what is on the page, the page is built again.</summary>
    string PageKey()
    {
        var plan = Engine.Plan;
        var c = Engine.Settings;
        return string.Join("|", page, plan?.Current?.Image, plan?.Current?.Start, plan?.Next?.Image, plan?.Next?.Start, plan?.Skipped?.Start,
            plan?.LockCurrent?.Image, plan?.LockPackage?.Id, plan?.Dark, Engine.Problem, Engine.LockProblem, c.Wallpaper.PackageId,
            c.Wallpaper.Enabled, c.LockScreen.Enabled, c.LockScreen.Source, c.LockScreen.PackageId, c.LockScreen.SignIn, c.PausedUntil, previewDate,
            Packages.LoadAll().Count);
    }

    Plan? PreviewPlan() => previewDate is { } date && date != DateOnly.FromDateTime(DateTime.Now)
        ? Schedule.Build(Engine.Settings, Engine.Package, Engine.Position, date.ToDateTime(new TimeOnly(12, 0)), lockPackage: Engine.LockPackage)
        : Engine.Plan;

    // ---------- Overview ----------

    void BuildOverview()
    {
        var plan = Engine.Plan;
        var c = Engine.Settings;
        foreach (var problem in new[] { Engine.Problem, c.LockScreen.Enabled ? Engine.LockProblem : null }.OfType<string>())
            BackgroundPanel.Children.Add(Warning(problem));
        if (ProfileNote() is { } note) BackgroundPanel.Children.Add(note);

        // What shows now, as two cards that open their settings: the whole card is the button.
        var tiles = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        tiles.ColumnDefinitions.Add(new ColumnDefinition());
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition());
        tiles.Children.Add(DesktopTile(plan));
        var lockTile = LockTile(plan);
        Grid.SetColumn(lockTile, 2);
        tiles.Children.Add(lockTile);
        BackgroundPanel.Children.Add(tiles);

        var card = new StackPanel();
        BackgroundPanel.Children.Add(new Border { Style = S("Card"), Child = card, Padding = new Thickness(24, 16, 24, 14), Margin = new Thickness(0, 0, 0, 20) });

        // The day: the sun, light and dark, and the pictures in the order they show, for today or another date. Its
        // actions on the right: Next image (today only), Back to the plan, and the date.
        var head = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        head.Children.Add(new TextBlock { Style = S("SectionLabel"), Text = Strings.T("THE DAY", "DER TAG"), VerticalAlignment = VerticalAlignment.Center });
        var picker = new DatePicker { Width = 170, SelectedDate = previewDate?.ToDateTime(TimeOnly.MinValue) }
            .Tip(Strings.T("Shows the plan for another day, for example in summer or winter. Changes nothing.",
                "Zeigt den Plan für einen anderen Tag, etwa im Sommer oder Winter. Ändert nichts."));
        picker.Loaded += (_, _) => SetDateHint(picker);
        picker.SelectedDateChanged += (_, _) =>
        {
            previewDate = picker.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null;
            Dispatcher.BeginInvoke(BuildBackground);
        };
        var dateRow = Ui.Horizontal();
        if (previewDate == null && c.Wallpaper.Enabled && Engine.Package != null && plan?.Current != null)
        {
            var paused = c.PausedUntil is { } until && until > DateTime.Now;
            if (plan.Skipped != null)
            {
                dateRow.Children.Add(Ui.Button(Strings.T("Back to the plan", "Zurück zum Plan"), (_, _) => Engine.BackToPlan(), "GhostButton")
                    .Tip(Strings.T("The picture that belongs to this moment again.", "Wieder das Bild, das zu diesem Moment gehört.")));
                dateRow.Children.Add(Ui.Spacer());
            }
            var skip = Ui.Button(Strings.T("Next image", "Nächstes Bild"), (_, _) => Engine.SkipNext())
                .Tip(paused ? Strings.T("Paused: resume first.", "Pausiert: erst fortsetzen.")
                    : Strings.T("Shows the next image of the day now and keeps to it until its own time; then the day goes on as planned.",
                        "Zeigt das nächste Bild des Tages sofort und bleibt dabei bis zu seiner eigenen Zeit; danach geht der Tag wie geplant weiter."));
            skip.IsEnabled = plan.Next != null && !paused;
            ToolTipService.SetShowOnDisabled(skip, true);
            dateRow.Children.Add(skip);
            dateRow.Children.Add(Ui.Spacer());
        }
        dateRow.Children.Add(picker);
        if (previewDate != null)
            dateRow.Children.Add(Ui.Button(Strings.T("Today", "Heute"), (_, _) =>
            {
                previewDate = null;
                BuildBackground();
            }).Tip(Strings.T("Back to today", "Zurück zu heute")));
        dateRow.HorizontalAlignment = HorizontalAlignment.Right;
        head.Children.Add(dateRow);
        card.Children.Add(head);

        timeline = new DayTimeline();
        timeline.Compact();
        timeline.Show(PreviewPlan(), Engine.Package, previewDate == null);
        timeline.ToolTip = Strings.T(
            "The curve is the sun over the day, the line in it the horizon. The band below shows the color mode, the strip the desktop's pictures in the order they show; with a package of its own for the lock screen, a second strip shows its pictures. Point anywhere on the day for the time, the sun's elevation and the pictures.",
            "Die Kurve ist die Sonne über den Tag, die Linie darin der Horizont. Das Band darunter zeigt den Farbmodus, der Streifen die Bilder des Desktops in der Reihenfolge, in der sie erscheinen; hat der Sperrbildschirm ein eigenes Paket, zeigt ein zweiter Streifen seine Bilder. Mit der Maus auf dem Tag stehen Uhrzeit, Sonnenhöhe und Bilder.");
        card.Children.Add(timeline);
        if (SunLine(PreviewPlan()) is { } sun) card.Children.Add(new TextBlock { Style = S("Muted"), Text = sun, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });

        var packagesHead = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        packagesHead.Children.Add(new TextBlock { Style = S("SectionLabel"), Text = Strings.T("YOUR PACKAGES", "DEINE PAKETE"), VerticalAlignment = VerticalAlignment.Center });
        var count = Packages.LoadAll().Count(p => !removing.Contains(p.Id));
        packagesHead.Children.Add(new TextBlock { Style = S("Muted"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Text = count == 1 ? Strings.T("1 package", "1 Paket") : Strings.T($"{count} packages", $"{count} Pakete") });
        BackgroundPanel.Children.Add(packagesHead);
        var grid = new UniformGrid { Columns = 5 };
        foreach (var p in Packages.LoadAll().Where(p => !removing.Contains(p.Id))) grid.Children.Add(PackageTile(p));
        grid.Children.Add(AddTile());
        BackgroundPanel.Children.Add(grid);
    }

    UIElement Warning(string text) => new Border
    {
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(14, 10, 14, 10),
        Margin = new Thickness(0, 0, 0, 12),
        Background = B("WarnBg"),
        Child = Ui.Horizontal(
            new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", Foreground = B("WarnText"), VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = text, Foreground = B("WarnText"), Margin = new Thickness(10, 0, 0, 0), TextWrapping = TextWrapping.Wrap }),
    };

    static string PictureName(PackageImage image) => image.DisplayName;

    Border Picture(string? path, double width, double height, int pixels = 320)
    {
        var box = new Border { Width = width, Height = height, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = B("Surface2") };
        if (path == null) return box;
        // Read off the window's thread: the box shows the picture as soon as it is there.
        void Show()
        {
            if (ThumbCache.Get(path, pixels) is { } image) box.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        }
        if (ThumbCache.Get(path, pixels, Show) != null) Show();
        return box;
    }

    UIElement DesktopTile(Plan? plan)
    {
        var c = Engine.Settings;
        var pkg = Engine.Package;
        string? path = null;
        string line, when;
        if (!c.Wallpaper.Enabled)
        {
            line = Strings.T("Off", "Aus");
            when = Strings.T("DynaTones does not change the background.", "DynaTones wechselt den Hintergrund nicht.");
        }
        else if (pkg == null || plan?.Current is not { } now)
        {
            line = Strings.T("No package yet", "Noch kein Paket");
            when = Strings.T("Choose one here or add one.", "Hier eins wählen oder hinzufügen.");
        }
        else
        {
            path = pkg.PathOf(pkg.Images[now.Image]);
            line = $"{pkg.DisplayName}, {PictureName(pkg.Images[now.Image])}";
            when = plan.Skipped is { } skipped
                ? Strings.T($"Ahead of its time, regularly from {Strings.Time(skipped.Start)}", $"Vorgezogen, regulär ab {Strings.Time(skipped.Start)}")
                : Strings.T($"Since {Strings.Time(now.Start)}", $"Seit {Strings.Time(now.Start)}");
            if (plan.Next is { } next)
                when += Strings.T($", next {PictureName(pkg.Images[next.Image])} from {Strings.Time(next.Start)}",
                    $", als Nächstes {PictureName(pkg.Images[next.Image])} ab {Strings.Time(next.Start)}");
        }
        return EntryCard("\uE7F4", Strings.T("Desktop", "Desktop"), line, when, Picture(path, 160, 90),
            Strings.T("Package, transition, fit, when each picture comes", "Paket, Übergang, Einpassen, wann jedes Bild kommt"), () => OpenPage(BgPage.Desktop));
    }

    UIElement LockTile(Plan? plan)
    {
        var l = Engine.Settings.LockScreen;
        string? path = null;
        string line, when;
        if (!l.Enabled)
        {
            line = Strings.T("Like Windows", "Wie Windows");
            when = Strings.T("DynaTones leaves it to Windows.", "DynaTones überlässt ihn Windows.");
        }
        else if (plan?.LockPackage is { } pkg && plan.LockCurrent is { } now)
        {
            path = pkg.PathOf(pkg.Images[now.Image]);
            line = $"{pkg.DisplayName}, {PictureName(pkg.Images[now.Image])}";
            when = l.Source == LockScreenSource.Wallpaper
                ? Strings.T("The desktop's picture", "Das Bild des Desktops")
                : Strings.T($"Since {Strings.Time(now.Start)}", $"Seit {Strings.Time(now.Start)}") +
                  (plan.LockNext is { } next ? Strings.T($", next from {Strings.Time(next.Start)}", $", nächstes ab {Strings.Time(next.Start)}") : "");
            if (l.SignIn && SignIn.IsInstalled) when += Strings.T(", also before signing in", ", auch vor der Anmeldung");
        }
        else
        {
            line = Strings.T("No package for it yet", "Noch kein Paket dafür");
            when = Strings.T("Choose one here.", "Hier eins wählen.");
        }
        var picture = Picture(path, 160, 90);
        if (path == null && !l.Enabled)
            picture.Child = new TextBlock { Style = S("Muted"), Text = Strings.T("Picture from Windows", "Bild aus Windows"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return EntryCard("\uE72E", Strings.T("Lock and sign-in screen", "Sperr- und Anmeldebildschirm"), line, when, picture,
            Strings.T("Like the desktop, a package of its own, or like Windows", "Wie der Desktop, ein eigenes Paket oder wie Windows"), () => OpenPage(BgPage.Lock));
    }

    /// <summary>
    /// One of the two cards on top: the picture shown now, what it is and when the next comes, and a chevron. The whole
    /// card is a button that opens the settings of it; it lifts on hover like every button.
    /// </summary>
    Button EntryCard(string glyph, string title, string line, string when, Border picture, string tip, Action open)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        picture.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(picture);
        var text = new StackPanel { Margin = new Thickness(16, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Ui.Horizontal(
            new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 0) },
            new TextBlock { Style = S("CardTitle"), Text = title, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }));
        text.Children.Add(new TextBlock { Style = S("RowLabel"), Text = line, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 6, 0, 0) });
        text.Children.Add(new TextBlock { Style = S("Muted"), Text = when, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var chevron = new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "\uE76C", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = B("Text4") };
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);
        var button = new Button
        {
            Style = S("SecondaryButton"),
            Height = double.NaN,
            Padding = new Thickness(14),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = grid,
        }.Tip(tip);
        button.Click += (_, _) => open();
        Speak(button, title);
        return button;
    }

    string? SunLine(Plan? plan)
    {
        if (plan?.Today is not { } day) return null;
        var rise = day.Crossing(Sun.SunriseElevation, true);
        var set = day.Crossing(Sun.SunriseElevation, false);
        var where = Engine.Position is { } p ? PlaceText(p) : Strings.T("no position", "kein Standort");
        return Strings.T(
            $"Sunrise {Fmt(rise)}, highest at {Strings.Time(day.SolarNoon)} with {Strings.Degrees(Math.Round(day.Highest, 1))}, sunset {Fmt(set)}. Location: {where}.",
            $"Sonnenaufgang {Fmt(rise)}, am höchsten um {Strings.Time(day.SolarNoon)} mit {Strings.Degrees(Math.Round(day.Highest, 1))}, Sonnenuntergang {Fmt(set)}. Standort: {where}.")
            + (Engine.Position?.IsRough == true
                ? Strings.T(" Estimated from the time zone, so the times can be off by up to half an hour.", " Aus der Zeitzone geschätzt, die Zeiten können bis zu einer halben Stunde abweichen.")
                : "");
    }

    /// <summary>The day in one picture: a slice of every picture side by side, from night to night.</summary>
    UIElement DayStrip(Package p, double height, int pixels = 240)
    {
        var shown = p.Images.Where(i => i.Anchor.Mode != AnchorMode.Off).ToList();
        if (shown.Count == 0) shown = p.Images;
        var paths = shown.Select(p.PathOf).ToList();
        return new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Background = B("Surface2"), Child = new StripView(paths, pixels) { Height = height } };
    }

    /// <summary>Each picture fills the whole strip, and only its own slice of it is drawn: the slices line up.</summary>
    sealed class StripView(IReadOnlyList<string> paths, int pixels) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0 || paths.Count == 0) return;
            var slice = w / paths.Count;
            for (var k = 0; k < paths.Count; k++)
            {
                // A picture still being read is drawn when it is there.
                if (ThumbCache.Get(paths[k], pixels, InvalidateVisual) is not { } image) continue;
                // Uniform to fill: the picture covers the strip, cut to its middle.
                var scale = Math.Max(w / image.Width, h / image.Height);
                var size = new Size(image.Width * scale, image.Height * scale);
                var place = new Rect((w - size.Width) / 2, (h - size.Height) / 2, size.Width, size.Height);
                dc.PushClip(new RectangleGeometry(new Rect(k * slice, 0, slice + 0.5, h)));
                dc.DrawImage(image, place);
                dc.Pop();
            }
        }
    }
    UIElement PackageTile(Package p)
    {
        var c = Engine.Settings;
        var uses = new List<string>();
        if (c.Wallpaper.PackageId == p.Id) uses.Add(Strings.T("Desktop", "Desktop"));
        if (c.LockScreen.Enabled && c.LockScreen.Source == LockScreenSource.Package && c.LockScreen.PackageId == p.Id) uses.Add(Strings.T("Lock screen", "Sperrbildschirm"));
        var stack = new StackPanel();
        stack.Children.Add(DayStrip(p, 60, 160));
        stack.Children.Add(new TextBlock { Style = S("RowLabel"), Text = p.DisplayName, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 6, 0, 0) });
        stack.Children.Add(new TextBlock
        {
            Style = S("Muted"),
            Text = uses.Count > 0 ? string.Join(Strings.T(" and ", " und "), uses) : p.AiMade ? Strings.T("made with AI", "mit KI erstellt") : Strings.T($"{p.Images.Count} images", $"{p.Images.Count} Bilder"),
            Foreground = uses.Count > 0 ? B("Accent") : B("Text4"),
        });
        var button = new Button
        {
            Style = S("SecondaryButton"),
            Height = double.NaN,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 12, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Content = stack,
        };
        if (uses.Count > 0) button.BorderBrush = B("Accent");
        button.Click += (_, _) => OpenPage(BgPage.Package, p.Id);
        Speak(button, p.DisplayName);
        return button;
    }

    UIElement AddTile()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Style = S("RowLabel"), Text = Strings.T("Add package", "Paket hinzufügen"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Style = S("Muted"), Text = Strings.T("Gallery, folder, file, new", "Galerie, Ordner, Datei, neu"), HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button
        {
            Style = S("SecondaryButton"),
            Height = double.NaN,
            MinHeight = 112,
            Margin = new Thickness(0, 0, 12, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = stack,
        };
        button.Click += (_, _) => ShowAddMenu(button);
        if (!Policies.UsersMayImport) PolicyLock.Apply(button, Policies.AllowUserImport);
        return button;
    }

    // ---------- Pages below the overview ----------

    StackPanel BackRow(string title)
    {
        var back = new Button { Style = S("SecondaryButton"), Content = Ui.Horizontal(
            new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = Strings.T("Back", "Zurück"), Margin = new Thickness(8, 0, 0, 0) }) };
        back.Click += (_, _) => OpenPage(page == BgPage.Image ? imageBack : BgPage.Overview);
        back.ToolTip = Strings.T("Back (Esc)", "Zurück (Esc)");
        Speak(back, Strings.T("Back", "Zurück"));
        var row = Ui.Horizontal(back, new TextBlock { Style = S("H2"), Text = title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) });
        row.Margin = new Thickness(0, 0, 0, 14);
        BackgroundPanel.Children.Add(row);
        return row;
    }

    void BuildDesktopPage()
    {
        BackRow(Strings.T("Desktop background", "Desktop-Hintergrund"));
        var c = Engine.Settings;
        var card = Ui.Card(BackgroundPanel, Strings.T("What the desktop shows", "Was der Desktop zeigt"));
        var obstacle = Wallpaper.Blocked() ?? Wallpaper.Competitor();
        Ui.Row(card, Strings.T("Change the background", "Hintergrund wechseln"),
            obstacle ?? Strings.T("Off: DynaTones only switches the color mode.", "Aus: DynaTones schaltet nur noch den Farbmodus."),
            Locked(Ui.Toggle(c.Wallpaper.Enabled, on => ChangeAndRebuild(x => x.Wallpaper.Enabled = on, BuildBackground)), Policies.WallpaperEnabled), showHelp: obstacle != null);
        var packages = Packages.LoadAll().Where(p => !removing.Contains(p.Id)).ToList();
        if (packages.Count > 0)
            Ui.Row(card, Strings.T("Package", "Paket"), Strings.T("The pictures of the desktop.", "Die Bilder des Desktops."),
                Locked(Ui.Choice(packages.Select(p => ((string?)p.Id, p.DisplayName)), c.Wallpaper.PackageId,
                    id => ChangeAndRebuild(x => x.Wallpaper.PackageId = id, BuildBackground), 260), Policies.ManagedPackageFolder));
        // The transition as four cards, as designed: at once, soft, gentle, flowing over a choice of minutes.
        card.Children.Add(new Border { Style = S("Divider") });
        var transitionHead = Ui.Horizontal(new TextBlock { Style = S("RowLabel"), Text = Strings.T("Transition to the next picture", "Übergang zum nächsten Bild") },
            new TextBlock { Style = S("InfoGlyph") });
        transitionHead.ToolTip = Strings.T("How one picture turns into the next. Flowing mixes the two in small steps before the change: the same place in other light then changes slowly, like the light itself. A package can have its own.",
            "Wie ein Bild ins nächste übergeht. Fließend mischt die beiden vor dem Wechsel in kleinen Schritten: Derselbe Ort in anderem Licht ändert sich dann langsam, wie das Licht selbst. Ein Paket kann einen eigenen haben.");
        transitionHead.Margin = new Thickness(0, 12, 0, 8);
        card.Children.Add(transitionHead);
        var cards = TransitionCards(Transitions.Code(c.Wallpaper.Fade, c.Wallpaper.BlendMinutes, c.Wallpaper.BlendSeconds), code => ChangeAndRebuild(x =>
        {
            var setting = Transitions.FromCode(code);
            x.Wallpaper.Fade = setting.Fade;
            x.Wallpaper.BlendMinutes = setting.BlendMinutes;
            x.Wallpaper.BlendSeconds = setting.BlendSeconds;
        }, BuildBackground));
        cards.Margin = new Thickness(0, 0, 0, 12);
        card.Children.Add(cards);
        if (Engine.Package?.Transition is { } own)
            Ui.Note(card, Strings.T($"\"{Engine.Package.DisplayName}\" has its own: {TransitionName(Transitions.Code(own.Fade, own.BlendMinutes, own.BlendSeconds))}. Set it on the package's page.",
                $"„{Engine.Package.DisplayName}“ hat einen eigenen: {TransitionName(Transitions.Code(own.Fade, own.BlendMinutes, own.BlendSeconds))}. Einstellbar auf der Seite des Pakets."));
        Ui.Row(card, Strings.T("Fit the picture", "Bild einpassen"),
            Strings.T("How a picture that does not match the screen is fitted.", "Wie ein Bild, das nicht zum Bildschirm passt, eingepasst wird."),
            Ui.Choice(new[]
            {
                (WallpaperFit.Fill, Strings.T("Fill", "Füllen")),
                (WallpaperFit.Fit, Strings.T("Fit", "Anpassen")),
                (WallpaperFit.Stretch, Strings.T("Stretch", "Strecken")),
                (WallpaperFit.Center, Strings.T("Center", "Zentrieren")),
                (WallpaperFit.Span, Strings.T("Span across monitors", "Über alle Bildschirme")),
            }, c.Wallpaper.Fit, f => Change(x => x.Wallpaper.Fit = f), 260, [
                Strings.T("Fills the screen, cuts off what sticks out", "Füllt den Bildschirm, schneidet Überstehendes ab"),
                Strings.T("Whole picture, with bars where it does not fill", "Ganzes Bild, mit Balken, wo es nicht füllt"),
                Strings.T("Pulls the picture to the screen's shape", "Zieht das Bild auf das Bildschirmformat"),
                Strings.T("Original size in the middle", "Originalgröße in der Mitte"),
                Strings.T("One picture across all monitors", "Ein Bild über alle Bildschirme"),
            ]));

        if (Engine.Package is { } pkg)
        {
            MirrorRow(card, pkg);
            BackgroundPanel.Children.Add(new TextBlock { Style = S("SectionLabel"), Text = Strings.T("WHEN EACH PICTURE COMES", "WANN JEDES BILD KOMMT"), Margin = new Thickness(0, 6, 0, 8) });
            BackgroundPanel.Children.Add(ImageGrid(pkg));
            BackgroundPanel.Children.Add(new TextBlock { Style = S("Muted"), Text = Strings.T("A click on a picture sets when it begins.", "Ein Klick auf ein Bild stellt ein, wann es beginnt."), Margin = new Thickness(0, 4, 0, 0) });
        }
    }

    static string TransitionName(int code) => code switch
    {
        -1 => Strings.T("at once", "sofort"),
        0 => Strings.T("soft", "weich"),
        Transitions.Gentle => Strings.T("gentle", "sanft"),
        _ => Strings.T($"flowing over {code} minutes", $"fließend über {code} Minuten"),
    };

    /// <summary>The four transitions as cards to choose from; flowing with its minutes in the card.</summary>
    UniformGrid TransitionCards(int code, Action<int> set)
    {
        var grid = new UniformGrid { Columns = 4 };
        var flowing = code > 0 ? code : 30;
        (int Code, string Title, string Help)[] options =
        [
            (-1, Strings.T("At once", "Sofort"), Strings.T("in one go", "auf einen Schlag")),
            (0, Strings.T("Soft", "Weich"), Strings.T("about 1 second", "etwa 1 Sekunde")),
            (Transitions.Gentle, Strings.T("Gentle", "Sanft"), Strings.T("a few seconds", "einige Sekunden")),
            (flowing, Strings.T("Flowing", "Fließend"), ""),
        ];
        for (var i = 0; i < options.Length; i++)
        {
            var (value, title, help) = options[i];
            var selected = i == 3 ? code > 0 : code == value;
            var stack = new StackPanel();
            var radio = new RadioButton { Content = title, IsChecked = selected, GroupName = "transition-" + GetHashCode(), FontWeight = FontWeights.SemiBold };
            var chosen = value;
            radio.Checked += (_, _) => { if (!selected) set(chosen); };
            stack.Children.Add(radio);
            if (i < 3) stack.Children.Add(new TextBlock { Style = S("Muted"), Text = help, Margin = new Thickness(28, 2, 0, 0) });
            else
            {
                var minutes = Ui.Choice(new[] { (10, Strings.T("10 minutes", "10 Minuten")), (30, Strings.T("30 minutes", "30 Minuten")), (60, Strings.T("60 minutes", "60 Minuten")) },
                    flowing is 10 or 30 or 60 ? flowing : 30, m => set(m), 130);
                Speak(minutes, Strings.T("Flowing over", "Fließend über"));
                stack.Children.Add(Ui.Horizontal(new TextBlock { Style = S("Muted"), Text = Strings.T("over", "über"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(28, 0, 8, 0) }, minutes));
                ((FrameworkElement)stack.Children[^1]).Margin = new Thickness(0, 4, 0, 0);
            }
            grid.Children.Add(new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                Margin = i == 0 ? new Thickness(0, 0, 6, 0) : i == 3 ? new Thickness(6, 0, 0, 0) : new Thickness(3, 0, 3, 0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(selected ? 2 : 1),
                BorderBrush = selected ? B("Accent") : B("Line"),
                Background = selected ? B("Selected") : B("Surface"),
                Child = stack,
            });
        }
        return grid;
    }

    /// <summary>The pictures of a package in the order of the day, with today's time of each.</summary>
    UIElement ImageGrid(Package p)
    {
        var day = new SunDay(DateOnly.FromDateTime(DateTime.Now), (Engine.Position ?? Locator.FromTimeZone())?.Latitude ?? 50,
            (Engine.Position ?? Locator.FromTimeZone())?.Longitude ?? 10);
        var slots = Planner.Day(p, day);
        var current = p.Id == Engine.Settings.Wallpaper.PackageId ? Engine.Plan?.Current?.Image : null;
        var grid = new UniformGrid { Columns = 8, Margin = new Thickness(-4, 0, -4, 0) };
        for (var i = 0; i < p.Images.Count; i++)
        {
            var index = i;
            var image = p.Images[i];
            var starts = slots.Where(s => s.Image == i).Select(s => Strings.Time(s.Start)).ToList();
            var stack = new StackPanel();
            var picture = Picture(p.PathOf(image), double.NaN, 54, 240);
            picture.CornerRadius = new CornerRadius(6);
            if (current == i)
            {
                picture.BorderBrush = B("Accent");
                picture.BorderThickness = new Thickness(2);
            }
            stack.Children.Add(picture);
            stack.Children.Add(new TextBlock { Style = S("RowLabel"), Text = PictureName(image), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(0, 4, 0, 0), FontWeight = current == i ? FontWeights.SemiBold : FontWeights.Normal });
            stack.Children.Add(new TextBlock
            {
                Style = S("Muted"),
                Text = image.Anchor.Mode == AnchorMode.Off ? Strings.T("not shown", "wird nicht gezeigt")
                    : starts.Count == 0 ? Strings.T("not today", "heute nicht")
                    : Strings.T("from ", "ab ") + string.Join(", ", starts) + (current == i ? Strings.T(", now", ", jetzt") : ""),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var button = new Button
            {
                Style = S("IconButton"),
                Width = double.NaN,
                Height = double.NaN,
                Padding = new Thickness(4),
                VerticalContentAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 4, 6),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                FontFamily = new FontFamily("Segoe UI"),
                Content = stack,
            };
            button.Click += (_, _) => OpenPage(BgPage.Image, p.Id, index);
            Speak(button, PictureName(image));
            grid.Children.Add(button);
        }
        return grid;
    }

    /// <summary>
    /// "Use evening pictures in the morning": only where it does something, a package with pictures Auto places and
    /// none of them in the morning. Packages with their own times (gallery, color packages) never show it.
    /// </summary>
    void MirrorRow(Panel card, Package p)
    {
        var auto = p.Images.Any(i => i.Anchor.Mode == AnchorMode.Auto);
        var morning = p.Images.Any(i =>
        {
            var (mode, elevation, rising, time) = Planner.Effective(i);
            return mode switch
            {
                AnchorMode.Time => time is { } t && t.Hour < 12,
                AnchorMode.Off => false,
                _ => rising && elevation < 20,
            };
        });
        if (!auto || morning) return;
        Ui.Row(card, Strings.T("Evening pictures in the morning too", "Abendbilder auch morgens"),
            Strings.T("The package has no morning pictures: its evening ones show in the morning too, the darkest first.",
                "Das Paket hat keine Morgenbilder: Seine Abendbilder kommen auch morgens, das dunkelste zuerst."),
            Ui.Toggle(p.MirrorEvening, on =>
            {
                p.MirrorEvening = on;
                SavePackage(p);
            }));
    }

    void BuildLockPage()
    {
        BackRow(Strings.T("Lock and sign-in screen", "Sperr- und Anmeldebildschirm"));
        var l = Engine.Settings.LockScreen;
        if (LockScreen.Blocked() is { } blocked) BackgroundPanel.Children.Add(Warning(blocked));
        else if (l.Enabled && Engine.LockProblem is { } problem) BackgroundPanel.Children.Add(Warning(problem));

        var fixedByPolicy = KitPolicy.IsFixed(Policies.LockScreenEnabled);
        var card = Ui.Card(BackgroundPanel, Strings.T("What the lock screen shows", "Was der Sperrbildschirm zeigt"));
        var choices = new UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 8) };
        var desktopPath = Engine.Package is { } dp && Engine.Plan?.Current is { } dc ? dp.PathOf(dp.Images[dc.Image]) : null;
        choices.Children.Add(LockChoice(desktopPath, Strings.T("Like the desktop", "Wie der Desktop"),
            Strings.T("The same pictures at the same moments.", "Dieselben Bilder zur selben Zeit."),
            l.Enabled && l.Source == LockScreenSource.Wallpaper, () => SetLock(LockScreenSource.Wallpaper, null), null, fixedByPolicy, new Thickness(0, 0, 8, 0)));

        var packages = Packages.LoadAll().Where(p => !removing.Contains(p.Id)).ToList();
        var own = packages.FirstOrDefault(p => p.Id == l.PackageId) ?? packages.FirstOrDefault();
        var ownPath = l.Enabled && l.Source == LockScreenSource.Package && Engine.Plan?.LockPackage is { } lp && Engine.Plan.LockCurrent is { } lc
            ? lp.PathOf(lp.Images[lc.Image]) : own != null && own.Images.Count > 0 ? own.PathOf(own.Images[0]) : null;
        FrameworkElement? picker = null;
        if (packages.Count > 0)
            picker = Ui.Choice(packages.Select(p => ((string?)p.Id, p.DisplayName)), own?.Id,
                id => SetLock(LockScreenSource.Package, id), 220);
        choices.Children.Add(LockChoice(ownPath, Strings.T("A package of its own", "Eigenes Paket"),
            Strings.T("Runs through the day by its own times.", "Läuft nach seinen eigenen Zeiten durch den Tag."),
            l.Enabled && l.Source == LockScreenSource.Package, () => SetLock(LockScreenSource.Package, own?.Id), picker, fixedByPolicy || packages.Count == 0, new Thickness(4, 0, 4, 0)));

        var link = new TextBlock { Style = S("Muted"), TextWrapping = TextWrapping.Wrap };
        link.Inlines.Add(Strings.T("DynaTones leaves it alone. ", "DynaTones lässt ihn in Ruhe. "));
        var hyperlink = new Hyperlink(new Run(Strings.T("Set it in Windows", "In Windows einstellen")));
        hyperlink.Click += (_, _) => Process.Start(new ProcessStartInfo("ms-settings:lockscreen") { UseShellExecute = true });
        link.Inlines.Add(hyperlink);
        choices.Children.Add(LockChoice(null, Strings.T("Like Windows", "Wie Windows"), null, !l.Enabled, () => SetLock(null, null), link, fixedByPolicy, new Thickness(8, 0, 0, 0)));
        card.Children.Add(choices);
        if (fixedByPolicy) Ui.Note(card, KitPolicy.LockedText);

        if (!l.Enabled) return;
        var signInReady = SignIn.IsInstalled;
        // Not set up yet (an installation without the admin tier, or one from before 0.2.0 that was only updated since):
        // one button sets it up with a single UAC prompt and switches it on, unless a policy has fixed it.
        FrameworkElement signIn = Locked(Ui.Toggle(l.SignIn, on => ChangeAndRebuild(c => c.LockScreen.SignIn = on, BuildBackground)), Policies.SignInScreen);
        // Set up by an earlier version whose task cannot yet give the picture back: set up again, one UAC prompt.
        var outdated = signInReady && !SignIn.ScriptCurrent;
        if ((!signInReady || outdated) && signIn.IsEnabled)
            signIn = Ui.Button(outdated ? Strings.T("Update", "Aktualisieren") : Strings.T("Set up", "Einrichten"), async (s, _) =>
            {
                ((UIElement)s).IsEnabled = false;
                await Task.Run(SignIn.Setup);
                SignIn.Recheck();
                if (SignIn.IsInstalled && !outdated) ChangeAndRebuild(c => c.LockScreen.SignIn = true, BuildBackground);
                else BuildBackground();
            }).Tip(Strings.T("Asks for admin rights once. Then it works for every user of this PC.",
                "Fragt einmal nach Adminrechten. Danach gilt es für alle Benutzer dieses PCs."));
        else if (!signInReady) signIn.IsEnabled = false;
        Ui.Row(card, Strings.T("Also show it before signing in", "Auch vor der Anmeldung zeigen"),
            outdated
                ? Strings.T("Set up by an earlier version. Updating asks for admin rights once.",
                    "Von einer früheren Version eingerichtet. Aktualisieren fragt einmal nach Adminrechten.")
            : !signInReady
                ? Strings.T("Needs a one-time setup with admin rights: two tasks of Windows put the picture there for every user.",
                    "Braucht einmal Adminrechte: Zwei Aufgaben von Windows setzen das Bild dort für alle Benutzer.")
                : l.SignIn && SignIn.NotShownYet()
                    ? Strings.T("Windows has not shown it yet. It does so the next time the PC is locked or starts; some editions of Windows leave it out.",
                        "Windows hat es noch nicht gezeigt. Das geschieht beim nächsten Sperren oder Start; manche Ausgaben von Windows lassen es aus.")
                    : Strings.T("The same picture before anyone signs in, for every user of this PC.",
                        "Dasselbe Bild, bevor sich jemand anmeldet, für alle Benutzer dieses PCs."),
            signIn, showHelp: !signInReady || outdated || (l.SignIn && SignIn.NotShownYet()));
    }

    UIElement LockChoice(string? path, string title, string? help, bool selected, Action choose, FrameworkElement? extra, bool locked, Thickness margin)
    {
        var stack = new StackPanel();
        var picture = Picture(path, double.NaN, 110, 320);
        picture.CornerRadius = new CornerRadius(6);
        if (path == null) picture.Child = new TextBlock { Style = S("Muted"), Text = Strings.T("Picture from Windows", "Bild aus Windows"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(picture);
        var radio = new RadioButton { Content = title, IsChecked = selected, GroupName = "lock", Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.SemiBold, IsEnabled = !locked };
        radio.Checked += (_, _) =>
        {
            if (!selected) choose();
        };
        stack.Children.Add(radio);
        if (help != null) stack.Children.Add(new TextBlock { Style = S("Muted"), Text = help, TextWrapping = TextWrapping.Wrap });
        if (extra != null)
        {
            // Full width under the text, aligned with it.
            extra.Margin = new Thickness(0, 8, 0, 0);
            extra.IsEnabled = !locked;
            if (extra is ComboBox)
            {
                extra.Width = double.NaN;
                extra.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            stack.Children.Add(extra);
        }
        return new Border
        {
            Padding = new Thickness(12),
            Margin = margin,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(selected ? 2 : 1),
            BorderBrush = selected ? B("Accent") : B("Line"),
            Background = selected ? B("Selected") : B("Surface"),
            Child = stack,
        };
    }

    /// <summary>
    /// What the lock screen shows. Windows spotlight covers any picture set there, so choosing one of DynaTones' turns
    /// it off and "Like Windows" gives it back as it was; no switch of its own for something that is a precondition.
    /// </summary>
    void SetLock(LockScreenSource? source, string? packageId)
    {
        var l = Engine.Settings.LockScreen;
        if (source != null && LockScreen.SpotlightOn && !l.TurnOffSpotlight)
        {
            var before = LockScreen.SpotlightValue();
            if (!DayEngine.DryRun) LockScreen.SetSpotlight(0);
            Change(c =>
            {
                c.LockScreen.TurnOffSpotlight = true;
                c.LockScreen.SpotlightBefore = before;
            });
        }
        else if (source == null && l.TurnOffSpotlight)
        {
            if (!DayEngine.DryRun) LockScreen.SetSpotlight(l.SpotlightBefore);
            Change(c =>
            {
                c.LockScreen.TurnOffSpotlight = false;
                c.LockScreen.SpotlightBefore = null;
            });
        }
        ChangeAndRebuild(c =>
        {
            c.LockScreen.Enabled = source != null;
            if (source is { } s) c.LockScreen.Source = s;
            if (packageId != null) c.LockScreen.PackageId = packageId;
        }, BuildBackground);
    }

    void BuildImagePage(Package p, int index)
    {
        var image = p.Images[index];
        BackRow(PictureName(image));
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        BackgroundPanel.Children.Add(new Border { Style = S("Card"), Child = grid, Padding = new Thickness(24, 20, 24, 20) });

        var left = new StackPanel { Width = 420 };
        left.Children.Add(Picture(p.PathOf(image), 420, 236, 840));
        left.Children.Add(new TextBlock { Style = S("Muted"), Text = Strings.T($"{image.File}, picture {index + 1} of {p.Images.Count} in {p.DisplayName}", $"{image.File}, Bild {index + 1} von {p.Images.Count} in {p.DisplayName}"), Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
        left.Children.Add(new TextBlock { Style = S("RowLabel"), Text = Hours(p, index), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        grid.Children.Add(left);

        var right = new StackPanel();
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        right.Children.Add(new TextBlock { Style = S("CardTitle"), Text = Strings.T("When this picture begins", "Wann dieses Bild beginnt"), Margin = new Thickness(0, 0, 0, 10) });
        var a = image.Anchor;

        var autoText = image.Suggested is { } s
            ? Strings.T($"DynaTones places it: {KindName(s.Kind)}, at {Strings.Degrees(s.Elevation)} with the sun {(s.Rising ? "rising" : "setting")}.",
                $"DynaTones setzt es: {KindName(s.Kind)}, bei {Strings.Degrees(s.Elevation)} mit {(s.Rising ? "steigender" : "fallender")} Sonne.")
              + (string.IsNullOrEmpty(s.ReasonShown) ? "" : " " + char.ToUpper(s.ReasonShown[0], Strings.Culture) + s.ReasonShown[1..] + ".")
            : Strings.T("DynaTones places it by what it found out about the picture.", "DynaTones setzt es nach dem, was es über das Bild herausgefunden hat.");
        right.Children.Add(AnchorChoice(Strings.T("Automatic", "Automatisch"), new TextBlock { Style = S("Muted"), Text = autoText, TextWrapping = TextWrapping.Wrap },
            a.Mode == AnchorMode.Auto, () => SetAnchor(p, image, x => x.Mode = AnchorMode.Auto)));

        var elevation = Ui.Field(a.Mode == AnchorMode.Sun ? a.Elevation.ToString("0.#", Strings.Culture) : (image.Suggested?.Elevation ?? 0).ToString("0.#", Strings.Culture), 56, text =>
        {
            if (!double.TryParse(text.Replace("°", ""), NumberStyles.Float, Strings.Culture, out var v) || v < -60 || v > 90) return false;
            SetAnchor(p, image, x =>
            {
                x.Mode = AnchorMode.Sun;
                x.Elevation = v;
            });
            return true;
        }).Tip(Strings.T("Degrees above the horizon: 0 is the horizon, 6 a low sun, -6 dusk, -12 nearly dark. Enter confirms.",
            "Grad über dem Horizont: 0 ist der Horizont, 6 tiefe Sonne, -6 Dämmerung, -12 fast dunkel. Enter übernimmt."));
        Speak(elevation, Strings.T("Sun's elevation in degrees", "Sonnenhöhe in Grad"));
        var rising = a.Mode == AnchorMode.Sun ? a.Rising : image.Suggested?.Rising ?? true;
        var side = Ui.Choice(new[] { (true, Strings.T("in the morning", "am Vormittag")), (false, Strings.T("in the afternoon", "am Nachmittag")) }, rising,
            r => SetAnchor(p, image, x =>
            {
                if (x.Mode != AnchorMode.Sun) x.Elevation = image.Suggested?.Elevation ?? 0;
                x.Mode = AnchorMode.Sun;
                x.Rising = r;
            }), 150);
        var sunLine = Ui.Horizontal(
                new TextBlock { Style = S("Muted"), Text = Strings.T("When the sun is", "Wenn die Sonne"), VerticalAlignment = VerticalAlignment.Center },
                Ui.Spacer(), elevation, Ui.Spacer(),
                new TextBlock { Style = S("Muted"), Text = Strings.T("degrees high,", "Grad hoch steht,"), VerticalAlignment = VerticalAlignment.Center },
                Ui.Spacer(), side);
        var sunBlock = new StackPanel();
        sunBlock.Children.Add(sunLine);
        sunBlock.Children.Add(new TextBlock { Style = S("Muted"), Text = Strings.T("Moves with the seasons.", "Wandert mit den Jahreszeiten."), Margin = new Thickness(0, 6, 0, 0) });
        right.Children.Add(AnchorChoice(Strings.T("With the sun", "Mit der Sonne"), sunBlock,
            a.Mode == AnchorMode.Sun, () => SetAnchor(p, image, x =>
            {
                x.Mode = AnchorMode.Sun;
                x.Elevation = image.Suggested?.Elevation ?? 0;
                x.Rising = image.Suggested?.Rising ?? true;
            })));

        var time = Ui.Field(a.Time ?? "12:00", 64, text =>
        {
            if (!Planner.TryTime(text, out var t)) return false;
            SetAnchor(p, image, x =>
            {
                x.Mode = AnchorMode.Time;
                x.Time = t.ToString("HH:mm", CultureInfo.InvariantCulture);
            });
            return true;
        }).Tip(Strings.T("A clock time like 7:30 or 19:45. Enter confirms.", "Eine Uhrzeit wie 7:30 oder 19:45. Enter übernimmt."));
        Speak(time, Strings.T("Clock time", "Uhrzeit"));
        right.Children.Add(AnchorChoice(Strings.T("At a clock time", "Zu einer Uhrzeit"), Ui.Horizontal(
                new TextBlock { Style = S("Muted"), Text = Strings.T("Every day from", "Jeden Tag ab"), VerticalAlignment = VerticalAlignment.Center }, Ui.Spacer(), time, Ui.Spacer(),
                new TextBlock { Style = S("Muted"), Text = Strings.T("o'clock", "Uhr"), VerticalAlignment = VerticalAlignment.Center }),
            a.Mode == AnchorMode.Time, () => SetAnchor(p, image, x =>
            {
                x.Mode = AnchorMode.Time;
                x.Time ??= "12:00";
            })));
        right.Children.Add(AnchorChoice(Strings.T("Do not show this picture", "Dieses Bild nicht zeigen"), null,
            a.Mode == AnchorMode.Off, () => SetAnchor(p, image, x => x.Mode = AnchorMode.Off)));
    }

    UIElement AnchorChoice(string title, UIElement? body, bool selected, Action choose)
    {
        var stack = new StackPanel();
        var radio = new RadioButton { Content = title, IsChecked = selected, GroupName = "anchor", FontWeight = FontWeights.SemiBold };
        radio.Checked += (_, _) =>
        {
            if (!selected) choose();
        };
        stack.Children.Add(radio);
        if (body is FrameworkElement f)
        {
            f.Margin = new Thickness(28, 4, 0, 0);
            stack.Children.Add(f);
        }
        return new Border
        {
            Padding = new Thickness(14, 10, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(selected ? 2 : 1),
            BorderBrush = selected ? B("Accent") : B("Line"),
            Background = selected ? B("Selected") : B("Surface"),
            Child = stack,
        };
    }

    /// <summary>When the picture shows today, and in June and December, so moving it with the sun is easy to judge.</summary>
    string Hours(Package p, int index)
    {
        var position = Engine.Position ?? Locator.FromTimeZone();
        string? On(DateOnly date)
        {
            var day = new SunDay(date, position?.Latitude ?? 50, position?.Longitude ?? 10);
            var slots = Planner.Day(p, day);
            var mine = slots.FirstOrDefault(s => s.Image == index);
            if (mine == null) return null;
            var end = slots.FirstOrDefault(s => s.Start > mine.Start)?.Start;
            return end == null ? Strings.T($"from {Strings.Time(mine.Start)}", $"ab {Strings.Time(mine.Start)}")
                : Strings.T($"{Strings.Time(mine.Start)} to {Strings.Time(end.Value)}", $"{Strings.Time(mine.Start)} bis {Strings.Time(end.Value)}");
        }
        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = On(today);
        if (now == null) return Strings.T("Not shown today.", "Heute nicht zu sehen.");
        var june = On(new DateOnly(today.Year, 6, 21));
        var december = On(new DateOnly(today.Year, 12, 21));
        return Strings.T($"Today {now}.", $"Heute {now}.")
            + (june != null && december != null ? Strings.T($" In June {june}, in December {december}.", $" Im Juni {june}, im Dezember {december}.") : "");
    }

    void SetAnchor(Package p, PackageImage image, Action<Anchor> change)
    {
        change(image.Anchor);
        SavePackage(p);
    }

    /// <summary>Saves a package and, when it is on screen, puts the screen right; the page is built again after the click.</summary>
    void SavePackage(Package p)
    {
        Packages.Save(p);
        var c = Engine.Settings;
        if (p.Id == c.Wallpaper.PackageId || (c.LockScreen.Source == LockScreenSource.Package && p.Id == c.LockScreen.PackageId))
        {
            Engine.Reload();
            Engine.Resolve("anchor");
        }
        Dispatcher.BeginInvoke(BuildBackground);
    }

    static string KindName(ImageKind kind) => kind switch
    {
        ImageKind.Dawn => Strings.T("morning", "Morgen"),
        ImageKind.Day => Strings.T("day", "Tag"),
        ImageKind.Dusk => Strings.T("evening", "Abend"),
        _ => Strings.T("night", "Nacht"),
    };

    // ---------- Adding and removing ----------

    void ShowAddMenu(FrameworkElement owner)
    {
        if (!Policies.UsersMayImport) return;
        var items = new List<(string, Action)>();
        if (Gallery.Source != null) items.Add((Strings.T("From the gallery", "Aus der Galerie"), OpenGallery));
        items.Add((Strings.T("Folder of pictures", "Ordner mit Bildern"), ImportFolder));
        items.Add((Strings.T("File (.zip or .ddw)", "Datei (.zip oder .ddw)"), ImportFile));
        items.Add((Strings.T("New package of your own", "Neues eigenes Paket"), NewPackage));
        ShowMenu(owner, items.ToArray());
    }

    void AddPackage_Click(object sender, RoutedEventArgs e) => ShowAddMenu(AddButton);

    void ImportFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.T("Folder with pictures", "Ordner mit Bildern") };
        if (dialog.ShowDialog(this) == true) _ = ImportAsync(dialog.FolderName);
    }

    void ImportFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Strings.T("Image packages", "Bildpakete") + " (*.zip;*.ddw)|*.zip;*.ddw" };
        if (dialog.ShowDialog(this) == true) _ = ImportAsync(dialog.FileName);
    }

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            TabBackground.IsChecked = true;
            _ = ImportAsync(files[0]);
        }
    }

    /// <summary>Locks a control when an organization fixed one of the policies behind it.</summary>
    static T Locked<T>(T element, params string[] policies) where T : FrameworkElement
    {
        PolicyLock.Apply(element, policies);
        return element;
    }

    async Task ImportAsync(string source)
    {
        if (importing || !Policies.UsersMayImport) return;
        importing = true;
        SummaryText.Text = Strings.T("Copying and looking at the pictures…", "Bilder werden kopiert und angesehen…");
        try
        {
            var package = await Sta.Run(() => Packages.Import(source));
            // The first package is used right away; later ones wait for "Use for the desktop".
            if (Engine.Package == null) Change(c => c.Wallpaper.PackageId = package.Id);
            OpenPackage(package.Id);
        }
        catch (Exception ex)
        {
            Log.Error($"Import of {source} failed", ex);
            _ = dialog.ShowMessageAsync(Strings.T("The pictures could not be added", "Die Bilder konnten nicht hinzugefügt werden"),
                ex.Message + Strings.T(" Nothing was changed; the other packages stay as they are.",
                    " Es wurde nichts geändert, die anderen Pakete bleiben, wie sie sind."), problem: true);
        }
        finally
        {
            importing = false;
            RefreshBackground();
        }
    }

    async Task DeletePackageAsync(Package package)
    {
        // Asked first: the times set by hand would be gone. Then Undo for a few seconds on top.
        var ok = await dialog.ConfirmAsync(
            Strings.T($"Delete \"{package.DisplayName}\"?", $"„{package.DisplayName}“ löschen?"),
            Strings.T("The package and its copied pictures leave DynaTones, with the times you set. The original files stay where they are.",
                "Das Paket und die kopierten Bilder verschwinden aus DynaTones, mit den Zeiten, die du gesetzt hast. Die Originaldateien bleiben, wo sie sind."),
            Strings.T("Delete", "Löschen"), danger: true);
        if (!ok) return;

        var wasDesktop = package.Id == Engine.Settings.Wallpaper.PackageId;
        var wasLock = package.Id == Engine.Settings.LockScreen.PackageId && Engine.Settings.LockScreen.Source == LockScreenSource.Package;
        removing.Add(package.Id);
        if (wasDesktop) Change(c => c.Wallpaper.PackageId = null);
        if (wasLock) Change(c => c.LockScreen.Source = LockScreenSource.Wallpaper);
        OpenPage(BgPage.Overview);
        dialog.ShowUndo(Strings.T($"\"{package.DisplayName}\" deleted.", $"„{package.DisplayName}“ gelöscht."),
            undo: () =>
            {
                removing.Remove(package.Id);
                if (wasDesktop) Change(c => c.Wallpaper.PackageId = package.Id);
                if (wasLock) Change(c => c.LockScreen.Source = LockScreenSource.Package);
                BuildBackground();
            },
            expired: () =>
            {
                removing.Remove(package.Id);
                Packages.Delete(package);
            });
    }

    // ---------- Header line and dates ----------

    string Summary(bool paused)
    {
        if (paused)
            return TrayHost.IsForever(Engine.Settings.PausedUntil)
                ? Strings.T("Paused until you resume", "Pausiert, bis du fortsetzt")
                : Strings.T($"Paused until {Strings.Time(Engine.Settings.PausedUntil!.Value)}", $"Pausiert bis {Strings.Time(Engine.Settings.PausedUntil!.Value)}");
        // A sentence, not a row of fragments: "Light mode, next change at 14:50".
        var parts = new List<string>();
        if (Engine.Plan?.Dark is { } dark) parts.Add(dark ? Strings.T("dark mode", "dunkler Modus") : Strings.T("light mode", "heller Modus"));
        if (Engine.Plan?.Blend is { } blend && Engine.Settings.Wallpaper.Enabled)
            parts.Add(Strings.T($"flowing into the next picture ({blend.Amount * 100:0} %)", $"geht ins nächste Bild über ({blend.Amount * 100:0} %)"));
        if (Engine.Plan?.NextAnnounced is { } next)
            parts.Add(next.Date == DateTime.Today
                ? Strings.T($"next change at {Strings.Time(next)}", $"nächster Wechsel um {Strings.Time(next)}")
                : Strings.T($"next change {next.ToString("dddd", Strings.Culture)} at {Strings.Time(next)}",
                    $"nächster Wechsel {next.ToString("dddd", Strings.Culture)} um {Strings.Time(next)}"));
        var line = string.Join(", ", parts);
        if (line.Length == 0) return Engine.Package == null ? Strings.T("Add a package to begin.", "Füge ein Paket hinzu, um zu beginnen.") : "";
        return char.ToUpper(line[0], Strings.Culture) + line[1..];
    }

    static string Fmt(DateTime? t) => t is { } v ? Strings.Time(v) : "–";

    static string SourceName(LocationSource s) => s switch
    {
        LocationSource.Windows => Strings.T("Windows location", "Windows-Standort"),
        LocationSource.WindowsCached => Strings.T("last Windows location", "letzter Windows-Standort"),
        LocationSource.Coordinates => Strings.T("entered", "eingegeben"),
        _ => Strings.T("time zone", "Zeitzone"),
    };

    /// <summary>
    /// WPF writes the empty date field's hint in the language of Windows, not in the one chosen here (seen 06.11.2026:
    /// "Datum auswählen" in an English window). The hint is internal to WPF, so it is set here when the field appears.
    /// </summary>
    static void SetDateHint(DatePicker picker)
    {
        if (picker.Template?.FindName("PART_TextBox", picker) is not DatePickerTextBox box) return;
        Speak(box, Strings.T("Preview a date", "Datum ansehen"));
        var property = typeof(DatePickerTextBox).GetField("WatermarkProperty",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as DependencyProperty;
        if (property != null) box.SetValue(property, Strings.T("Pick a date", "Datum auswählen"));
    }
}
