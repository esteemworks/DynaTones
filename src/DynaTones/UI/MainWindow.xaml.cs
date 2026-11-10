using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

public partial class MainWindow : Window
{
    readonly TrayHost host;
    DayEngine Engine => host.Engine;
    DateOnly? previewDate;
    bool importing;
    bool galleryOpen;
    readonly KitDialog dialog;
    /// <summary>Packages removed but still waiting for Undo: hidden, their files not deleted yet.</summary>
    readonly HashSet<string> removing = [];

    public MainWindow(TrayHost host)
    {
        this.host = host;
        InitializeComponent();
        // The family's window from the kit: title bar, title, actions, tabs with the gear (STANDARD, "The window").
        Frame.Attach(this);
        TabBackground = Frame.AddTab("background", "");
        TabMode = Frame.AddTab("mode", "");
        TabProfiles = Frame.AddTab("profiles", "");
        Frame.PageChanged += PageChanged;
        _ = new MiddleScroll(this, ScrollMarker);
        dialog = new KitDialog(DialogLayer);
        // A package waiting for Undo is really deleted when the window goes.
        Closed += (_, _) => dialog.Flush();
        ApplyTexts();
        BuildModePage();
        BuildSettingsPage();
        BuildBackground();
        Engine.Updated += RefreshBackground;
        Closed += (_, _) => Engine.Updated -= RefreshBackground;
        Engine.Updated += ShowProfileColor;
        Closed += (_, _) => Engine.Updated -= ShowProfileColor;
        ShowProfileColor();
        RefreshBackground();
        // A new installation: the introduction first; nothing changes in Windows until DynaTones is switched on.
        StartIntro();
    }

    /// <summary>
    /// With the accent "Profile", the window takes on the color of the profile it shows: the one being edited, else
    /// the one that applies now; without a profile DynaTones' own (STANDARD, "Appearance follows the profile").
    /// </summary>
    void ShowProfileColor()
    {
        var profile = editingProfile && shownProfile is { } id ? FamilyProfiles.Find(id) : Engine.ActiveProfile;
        Theme.Show(profile == null ? null : FamilyProfiles.ColorOf(FamilyProfiles.Current.Profiles, profile));
    }

    /// <summary>The pages, for the snapshot.</summary>
    internal IEnumerable<(string Name, System.Windows.Controls.Primitives.ToggleButton Button)> Tabs =>
    [
        ("background", TabBackground), ("mode", TabMode), ("profiles", TabProfiles), ("settings", GearButton),
    ];

    /// <summary>For the snapshot: a question with the Undo bar under it, as deleting a package shows them; or none.</summary>
    internal void ShowSampleDialog(bool show)
    {
        if (!show)
        {
            dialog.Close();
            dialog.Flush();
            return;
        }
        dialog.ShowUndo(Strings.T("\"Sample\" deleted.", "„Beispiel“ gelöscht."), () => { });
        _ = dialog.ConfirmAsync(Strings.T("Delete \"Sample\"?", "„Beispiel“ löschen?"),
            Strings.T("The package and its copied images leave DynaTones, with the anchors you set. The original files stay where they are.",
                "Das Paket und die kopierten Bilder verschwinden aus DynaTones, mit den Ankern, die du gesetzt hast. Die Originaldateien bleiben, wo sie sind."),
            Strings.T("Delete", "Löschen"), danger: true);
    }

    readonly RadioButton TabBackground, TabMode, TabProfiles;
    System.Windows.Controls.Primitives.ToggleButton GearButton => Frame.Gear;
    TextBlock SummaryText => Frame.Summary;

    void ApplyTexts()
    {
        AddButtonText.Text = Strings.T("Add package", "Paket hinzufügen");
        TabBackground.Content = Strings.T("Background", "Hintergrund");
        TabMode.Content = Strings.T("Color mode", "Farbmodus");
        TabProfiles.Content = Strings.T("Profiles", "Profile");
        Frame.ApplyTexts();
        // Names for screen readers: icon buttons and buttons made of an icon and a text get none from WPF.
        RefreshButton.ToolTip = Strings.T(
            "Update now (F5): reads the settings and the location again and puts everything right for this moment. DynaTones does this by itself; this is for when something else got in the way.",
            "Jetzt aktualisieren (F5): liest Einstellungen und Standort neu und setzt alles so, wie es jetzt sein soll. Das macht DynaTones von selbst; das hier ist für den Fall, dass etwas dazwischenkam.");
        Speak(RefreshButton, Strings.T("Update now", "Jetzt aktualisieren"));
        Speak(AddButton, AddButtonText.Text);
        PauseButton.ToolTip = Strings.T(
            "Stops changing the background and the color mode for a while; what is on screen stays.",
            "Wechselt eine Zeit lang weder Hintergrund noch Farbmodus; was zu sehen ist, bleibt.");
        AddButton.ToolTip = Strings.T(
            "From the gallery, a folder of images or a .zip or .ddw file. A file can also be dropped on this window.",
            "Aus der Galerie, aus einem Ordner mit Bildern oder aus einer .zip- oder .ddw-Datei. Eine Datei lässt sich auch auf dieses Fenster ziehen.");
        // An organization that does not let users add packages: the button stays, greyed out, and says why.
        if (!Policies.UsersMayImport) PolicyLock.Apply(AddButton, Policies.AllowUserImport);
        TabBackground.ToolTip = Strings.T("What shows now, the desktop and the lock screen, your packages", "Was jetzt zu sehen ist, Desktop und Sperrbildschirm, deine Pakete");
        TabMode.ToolTip = Strings.T("Light and dark for apps, Windows and the mouse pointer", "Hell und dunkel für Apps, Windows und den Mauszeiger");
        TabProfiles.ToolTip = Strings.T("Other settings at other times, shared with the EsteemWorks apps", "Andere Einstellungen zu anderen Zeiten, geteilt mit den EsteemWorks-Apps");

        GalleryBackText.Text = Strings.T("Back", "Zurück");
        GalleryTitle.Text = Strings.T("Gallery", "Galerie");
        GalleryHelp.Text = Strings.T(
            "Each package shows one place from night through the day to night, or one color after the other. Add it, and it is set up " +
            "for the day already. Packages marked as made with AI show invented places.",
            "Jedes Paket zeigt einen Ort von der Nacht durch den Tag bis zur Nacht oder eine Farbe nach der anderen. Hinzugefügt ist es " +
            "schon auf den Tag eingestellt. Mit KI erstellte Pakete zeigen erfundene Orte.");
    }

    // ---------- Header ----------

    /// <summary>The name a screen reader reads for <paramref name="element"/>.</summary>
    static void Speak(DependencyObject element, string name) => System.Windows.Automation.AutomationProperties.SetName(element, name);

    void Refresh_Click(object sender, RoutedEventArgs e) => _ = Motion.SpinWhile(RefreshTurn, () =>
    {
        Engine.Reload();
        Engine.Resolve("request");
        return Task.CompletedTask;
    });

    /// <summary>Paused: one click resumes. Otherwise how long, from a menu.</summary>
    void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (Engine.Settings.PausedUntil is { } until && until > DateTime.Now)
        {
            host.Pause(null);
            return;
        }
        ShowMenu(PauseButton,
            (Strings.T("For an hour", "Für eine Stunde"), () => host.Pause(DateTime.Now.AddHours(1))),
            (Strings.T("Until tomorrow morning", "Bis morgen früh"), () => host.Pause(TrayHost.TomorrowMorning)),
            (Strings.T("Until I resume", "Bis ich fortsetze"), () => host.Pause(TrayHost.Forever)));
    }

    /// <summary>A small menu under a button.</summary>
    static void ShowMenu(FrameworkElement owner, params (string Text, Action Click)[] items)
    {
        var menu = new ContextMenu { PlacementTarget = owner, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var (text, click) in items)
        {
            var item = new MenuItem { Header = text };
            item.Click += (_, _) => click();
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>A tab was chosen or the settings opened or closed (the kit's frame keeps the rules of tabs and gear).</summary>
    void PageChanged()
    {
        if (BackgroundView == null) return;
        // A tab always opens on its own first page; the gallery and the pages under Background start from there.
        if (Frame.Page != null && (galleryOpen || page != BgPage.Overview))
        {
            galleryOpen = false;
            OpenPage(BgPage.Overview);
        }
        ShowPage();
    }

    void ShowPage()
    {
        var settings = GearButton.IsChecked == true;
        Visibility Shown(bool on) => on ? Visibility.Visible : Visibility.Collapsed;
        BackgroundView.Visibility = Shown(!settings && TabBackground.IsChecked == true && !galleryOpen);
        GalleryView.Visibility = Shown(!settings && TabBackground.IsChecked == true && galleryOpen);
        ModeView.Visibility = Shown(!settings && TabMode.IsChecked == true);
        var profiles = !settings && TabProfiles.IsChecked == true;
        if (profiles && ProfilesView.Visibility != Visibility.Visible) BuildProfilesPage();
        ProfilesView.Visibility = Shown(profiles);
        SettingsView.Visibility = Shown(settings);
    }

    void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (dialog.IsOpen) return;
        if (e.Key == System.Windows.Input.Key.F5)
        {
            Refresh_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.N && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            AddPackage_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape && GearButton.IsChecked == true)
        {
            GearButton.IsChecked = false;
            e.Handled = true;
        }
        // Esc on a page below Background or in the gallery: one step back.
        else if (e.Key == System.Windows.Input.Key.Escape && TabBackground.IsChecked == true && (galleryOpen || page != BgPage.Overview))
        {
            if (galleryOpen) GalleryBack_Click(sender, e);
            else OpenPage(page == BgPage.Image ? imageBack : BgPage.Overview);
            e.Handled = true;
        }
    }

    // ---------- Gallery ----------

    GalleryCatalog? catalog;
    /// <summary>Packages being downloaded right now, by gallery id.</summary>
    readonly HashSet<string> adding = [];
    bool galleryLoading;

    void OpenGallery()
    {
        TabBackground.IsChecked = true;
        if (GearButton.IsChecked == true) GearButton.IsChecked = false;
        galleryOpen = true;
        ShowPage();
        _ = LoadGalleryAsync();
    }

    void GalleryBack_Click(object sender, RoutedEventArgs e)
    {
        galleryOpen = false;
        OpenPage(BgPage.Overview);
        ShowPage();
    }

    /// <summary>For the snapshot: the gallery open, with its list loaded.</summary>
    internal Task ShowGalleryForSnapshot()
    {
        TabBackground.IsChecked = true;
        galleryOpen = true;
        ShowPage();
        return LoadGalleryAsync();
    }

    async Task LoadGalleryAsync()
    {
        if (Gallery.Source == null)
        {
            GalleryNote(Strings.T("Your organization switched the gallery off.", "Deine Organisation hat die Galerie ausgeschaltet."));
            return;
        }
        if (catalog == null)
        {
            if (galleryLoading) return;
            galleryLoading = true;
            GalleryNote(Strings.T("Loading the gallery…", "Galerie wird geladen…"));
            try
            {
                catalog = await Gallery.LoadAsync();
            }
            catch (Exception ex)
            {
                Log.Warning($"Gallery {Gallery.Source} could not be loaded: {ex.Message}");
                GalleryNote(Strings.T("The gallery could not be reached. Packages you have are not affected; try again later.",
                    "Die Galerie war nicht erreichbar. Deine Pakete sind davon nicht betroffen; versuch es später noch einmal."));
                return;
            }
            finally
            {
                galleryLoading = false;
            }
        }
        BuildGalleryCards();
    }

    void GalleryNote(string text)
    {
        GalleryCards.Children.Clear();
        GalleryCards.Columns = 1;
        GalleryCards.Children.Add(new TextBlock { Style = (Style)FindResource("Muted"), Text = text, TextWrapping = TextWrapping.Wrap });
    }

    void BuildGalleryCards()
    {
        if (catalog == null) return;
        GalleryCards.Children.Clear();
        GalleryCards.Columns = 3;
        if (catalog.Packages.Count == 0)
        {
            GalleryNote(Strings.T("The gallery has no packages right now.", "Die Galerie hat gerade keine Pakete."));
            return;
        }
        var installed = Packages.LoadAll().Where(p => p.GalleryId != null && !removing.Contains(p.Id))
            .GroupBy(p => p.GalleryId!).ToDictionary(g => g.Key, g => g.First());
        foreach (var entry in catalog.Packages)
            GalleryCards.Children.Add(GalleryCard(entry, installed.GetValueOrDefault(entry.Id)));
    }

    UIElement GalleryCard(GalleryEntry entry, Package? installed)
    {
        // The action docks at the bottom, so the cards of a row end on one line whatever their texts.
        var dock = new DockPanel { LastChildFill = true };
        var stack = new StackPanel();
        var preview = new Border { Height = 174, CornerRadius = new CornerRadius(6), ClipToBounds = true, Background = (Brush)FindResource("Surface2") };
        if (entry.PreviewFile is { } file)
        {
            // Read off the window's thread, like every picture.
            void Show()
            {
                if (ThumbCache.Get(file, 640) is { } image) preview.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            }
            if (ThumbCache.Get(file, 640, Show) != null) Show();
        }
        stack.Children.Add(preview);
        stack.Children.Add(new TextBlock { Style = (Style)FindResource("RowLabel"), Text = entry.DisplayName, Margin = new Thickness(0, 10, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        if (entry.DescriptionShown is { Length: > 0 } description)
            stack.Children.Add(new TextBlock { Style = (Style)FindResource("Help"), Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });

        var facts = new List<string>();
        if (StyleName(entry.Style) is { Length: > 0 } style) facts.Add(style);
        if (entry.Images > 0) facts.Add(Strings.T($"{entry.Images} images", $"{entry.Images} Bilder"));
        if (entry.Size > 0) facts.Add($"{entry.Size / 1048576.0:0} MB");
        if (entry.AiMade) facts.Add(Strings.T("made with AI", "mit KI erstellt"));
        stack.Children.Add(new TextBlock { Style = (Style)FindResource("Muted"), Text = string.Join(", ", facts), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10) });

        UIElement action;
        if (adding.Contains(entry.Id))
            action = new TextBlock { Style = (Style)FindResource("Muted"), Text = Strings.T("Downloading and setting up…", "Wird geladen und eingerichtet…") };
        else if (installed != null)
        {
            var show = Ui.Button(Strings.T("Show", "Anzeigen"), (_, _) =>
            {
                galleryOpen = false;
                OpenPackage(installed.Id);
                ShowPage();
            }).Tip(Strings.T("Opens the package under Background.", "Öffnet das Paket unter Hintergrund."));
            action = Ui.Horizontal(new TextBlock { Style = (Style)FindResource("Muted"), Text = Strings.T("Added", "Hinzugefügt"), VerticalAlignment = VerticalAlignment.Center },
                Ui.Spacer(12), show);
        }
        else
        {
            var add = Ui.Button(Strings.T("Add", "Hinzufügen"), (_, _) => _ = AddFromGalleryAsync(entry), "PrimaryButton")
                .Tip(Strings.T("Downloads the package and sets it up for the day. The first package becomes the background.",
                    "Lädt das Paket und stellt es auf den Tag ein. Das erste Paket wird der Hintergrund."));
            Speak(add, Strings.T($"Add {entry.DisplayName}", $"{entry.DisplayName} hinzufügen"));
            action = add;
        }
        if (action is FrameworkElement f) f.HorizontalAlignment = HorizontalAlignment.Left;
        DockPanel.SetDock(action, Dock.Bottom);
        dock.Children.Add(action);
        dock.Children.Add(stack);
        return new Border { Style = (Style)FindResource("Card"), Child = dock, Margin = new Thickness(6, 0, 6, 12), Padding = new Thickness(12) };
    }

    static string StyleName(string? style) => style switch
    {
        "photo" => Strings.T("Photo style", "Fotostil"),
        "3d" => "3D",
        "2d" => Strings.T("2D, drawn flat", "2D, flach gezeichnet"),
        "drawn" => Strings.T("Drawn", "Gezeichnet"),
        "color" => Strings.T("Colors through the day", "Farben über den Tag"),
        _ => "",
    };

    async Task AddFromGalleryAsync(GalleryEntry entry)
    {
        if (!adding.Add(entry.Id)) return;
        BuildGalleryCards();
        try
        {
            var package = await Gallery.InstallAsync(entry, Sta.Run<Package>);
            // The first package is used right away, as with any import.
            if (Engine.Package == null) Change(c => c.Wallpaper.PackageId = package.Id);
            BuildBackground();
        }
        catch (Exception ex)
        {
            Log.Error($"Gallery package {entry.Id} could not be added", ex);
            _ = dialog.ShowMessageAsync(Strings.T("The package could not be added", "Das Paket konnte nicht hinzugefügt werden"),
                ex.Message + Strings.T(" Nothing was changed; the other packages stay as they are.",
                    " Es wurde nichts geändert, die anderen Pakete bleiben, wie sie sind."), problem: true);
        }
        finally
        {
            adding.Remove(entry.Id);
            BuildGalleryCards();
        }
    }

    // ---------- Color mode: UI/ModePage.cs ----------

    static TextBox TimeField(string value, Action<string> set) => Ui.Field(value, 72, text =>
    {
        if (!Planner.TryTime(text, out var t)) return false;
        set(t.ToString("HH:mm", CultureInfo.InvariantCulture));
        return true;
    });

    // ---------- Location ----------

    /// <summary>
    /// Where the sun is calculated, in the settings: set up once, rarely touched again. One card, as designed: the source
    /// on the right of the title, then a small map with the place as a name a person knows, today's sunrise and sunset
    /// and where the position came from; fields only for a place entered by hand, filled in.
    /// </summary>
    void BuildLocationCards(Panel parent)
    {
        var l = Engine.Settings.Location;
        var stack = new StackPanel();
        parent.Children.Add(new Border { Style = S("Card"), Child = stack, Padding = new Thickness(24, 16, 24, 18), Margin = new Thickness(0, 0, 0, 16) });
        var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = Ui.Horizontal(new TextBlock { Style = S("CardTitle"), Text = Strings.T("Location", "Standort"), VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Style = S("InfoGlyph") });
        title.ToolTip = Strings.T("DynaTones calculates the sun on this device. No internet, no account.", "DynaTones berechnet die Sonne auf diesem Gerät. Kein Internet, kein Konto.");
        title.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(title);
        // Windows' location strictly, an older choice, shows as the Windows location: the time zone is its fallback anyway.
        var shownMode = l.Mode == LocationMode.Windows ? LocationMode.Auto : l.Mode;
        var source = Locked(Ui.Segments(new[]
        {
            (LocationMode.Auto, Strings.T("Windows location", "Windows-Standort")),
            (LocationMode.Coordinates, Strings.T("Enter a place", "Ort eingeben")),
            (LocationMode.TimeZone, Strings.T("Time zone only", "Nur Zeitzone")),
        }, shownMode, SetLocationMode, [
            Strings.T("Where Windows says the device is; if Windows does not answer, a place entered before, then the time zone",
                "Wo Windows das Gerät sieht; antwortet Windows nicht, ein früher eingegebener Ort, dann die Zeitzone"),
            Strings.T("A city or coordinates you enter", "Eine Stadt oder Koordinaten, die du eingibst"),
            Strings.T("The main city of your time zone: needs nothing, but can be half an hour off", "Die Hauptstadt deiner Zeitzone: braucht nichts, kann aber eine halbe Stunde danebenliegen"),
        ]), Policies.LocationMode);
        Grid.SetColumn(source, 1);
        head.Children.Add(source);
        stack.Children.Add(head);

        var current = Engine.Position;
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        var map = new PlaceMap { Width = 200, Height = 110 };
        if (current != null) map.Show(current.Latitude, current.Longitude);
        body.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = B("Line"), BorderThickness = new Thickness(1), Child = map,
            VerticalAlignment = VerticalAlignment.Top });
        var facts = new StackPanel { Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        facts.Children.Add(new TextBlock { Style = S("H2"), Text = current == null ? Strings.T("No position yet", "Noch kein Standort") : PlaceText(current, withCountry: true), TextWrapping = TextWrapping.Wrap });
        if (Engine.Plan?.Today is { } day)
        {
            var rise = day.Crossing(Sun.SunriseElevation, true);
            var set = day.Crossing(Sun.SunriseElevation, false);
            facts.Children.Add(new TextBlock { Style = S("RowLabel"), Margin = new Thickness(0, 4, 0, 0),
                Text = Strings.T($"Sunrise {Fmt(rise)}, sunset {Fmt(set)}", $"Sonnenaufgang {Fmt(rise)}, Sonnenuntergang {Fmt(set)}") });
        }
        if (current != null)
        {
            var from = current.Source switch
            {
                LocationSource.Windows => Strings.T("From the Windows location, just now", "Vom Windows-Standort, gerade eben"),
                LocationSource.WindowsCached when l.LastWindows is { } w => Strings.T($"From the Windows location, {Ago(w.At)}", $"Vom Windows-Standort, {Ago(w.At)}"),
                LocationSource.WindowsCached => Strings.T("From the Windows location", "Vom Windows-Standort"),
                LocationSource.Coordinates => Strings.T("Entered by you", "Von dir eingegeben"),
                _ => Strings.T("Estimated from the time zone; the sun can be up to half an hour off", "Aus der Zeitzone geschätzt; die Sonne kann bis zu einer halben Stunde danebenliegen"),
            };
            var line = Ui.Horizontal(new TextBlock { Style = S("Muted"), Text = from, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            if (l.Mode is LocationMode.Auto or LocationMode.Windows)
            {
                var again = Ui.Button(Strings.T("Ask again", "Erneut fragen"), async (_, _) =>
                {
                    await Engine.RefreshLocationAsync(force: true);
                    BuildSettingsPage();
                }, "GhostButton").Tip(Strings.T("Asks Windows once more. On managed devices this is often switched off by policy.",
                    "Fragt Windows noch einmal. Auf verwalteten Geräten ist das oft per Richtlinie aus."));
                again.Margin = new Thickness(8, 0, 0, 0);
                line.Children.Add(again);
            }
            line.Margin = new Thickness(0, 2, 0, 0);
            facts.Children.Add(line);
        }
        Grid.SetColumn(facts, 1);
        body.Children.Add(facts);
        stack.Children.Add(body);
        if (Locator.LastWindowsProblem is { } problem && l.Mode is LocationMode.Auto or LocationMode.Windows)
            Ui.Note(stack, problem + Strings.T(" DynaTones uses the next source.", " DynaTones nimmt die nächste Quelle."));

        if (l.Mode != LocationMode.Coordinates) return;
        stack.Children.Add(new Border { Height = 14 });
        var card = stack;
        // About 32,000 places: the list draws only what is in view.
        var places = new ComboBox { Width = 300, IsEditable = true, IsTextSearchEnabled = true }
            .Tip(Strings.T("Type the first letters to jump to a city.", "Die ersten Buchstaben tippen, um zu einer Stadt zu springen."));
        places.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        VirtualizingPanel.SetIsVirtualizing(places, true);
        VirtualizingPanel.SetVirtualizationMode(places, VirtualizationMode.Recycling);
        places.ItemsSource = SortedTowns ??= Locator.Towns.OrderBy(z => z.Name, StringComparer.CurrentCultureIgnoreCase).ThenByDescending(z => z.People).ToList();
        // Coordinates typed in: the place they are in or near, so the field never stands empty.
        places.Text = l.PlaceName ?? (l.Latitude is { } la && l.Longitude is { } lo ? Locator.Near(la, lo, 25)?.Label : null) ?? "";
        places.SelectionChanged += (_, _) =>
        {
            if (places.SelectedItem is Locator.Town p)
                ChangeAndRebuild(c =>
                {
                    c.Location.Latitude = p.Latitude;
                    c.Location.Longitude = p.Longitude;
                    c.Location.PlaceName = p.Label;
                }, BuildSettingsPage);
        };
        Ui.Row(card, Strings.T("City", "Stadt"), Strings.T("Every place with at least 15,000 inhabitants; for any other place enter its coordinates.",
            "Jeder Ort mit mindestens 15.000 Einwohnern; für jeden anderen Ort seine Koordinaten eingeben."), places);
        var lat = Ui.Field(l.Latitude?.ToString("0.####", CultureInfo.InvariantCulture) ?? "", 100, text => SetCoordinate(text, true))
            .Tip(Strings.T("Latitude, -90 to 90. Enter confirms.", "Breite, -90 bis 90. Enter übernimmt."));
        Speak(lat, Strings.T("Latitude", "Breite"));
        var lon = Ui.Field(l.Longitude?.ToString("0.####", CultureInfo.InvariantCulture) ?? "", 100, text => SetCoordinate(text, false))
            .Tip(Strings.T("Longitude, -180 to 180. Enter confirms.", "Länge, -180 bis 180. Enter übernimmt."));
        Speak(lon, Strings.T("Longitude", "Länge"));
        Ui.Row(card, Strings.T("Latitude and longitude", "Breite und Länge"),
            Strings.T("In degrees, for example 52.21 and 8.36. South and west are negative.", "In Grad, etwa 52.21 und 8.36. Süd und West sind negativ."),
            Ui.Horizontal(lat, Ui.Spacer(), lon));
        Locked(places, Policies.Coordinates);
        Locked(lat, Policies.Coordinates);
        Locked(lon, Policies.Coordinates);
    }

    /// <summary>
    /// A fixed place starts from where DynaTones is now, so its fields are never empty; the city list or the
    /// coordinates then change it.
    /// </summary>
    void SetLocationMode(LocationMode mode) => ChangeAndRebuild(c =>
    {
        c.Location.Mode = mode;
        if (mode == LocationMode.Coordinates && (c.Location.Latitude == null || c.Location.Longitude == null) && Engine.Position is { } now)
        {
            c.Location.Latitude = Math.Round(now.Latitude, 4);
            c.Location.Longitude = Math.Round(now.Longitude, 4);
            c.Location.PlaceName = now.Source == LocationSource.TimeZone ? now.Description : null;
        }
    }, BuildSettingsPage);

    static List<Locator.Town>? SortedTowns;

    /// <summary>
    /// A position as a person knows it: the city picked, the place it is in or near ("Near Melle", up to 25 km), else
    /// the coordinates. The time zone's guess is its main city.
    /// </summary>
    static string PlaceText(Position p, bool withCountry = false)
    {
        if (p.Source is LocationSource.Coordinates or LocationSource.TimeZone && !p.Description.Contains('°')) return p.Description;
        if (Locator.Near(p.Latitude, p.Longitude, 25) is not { } near) return Locator.Format(p.Latitude, p.Longitude);
        var name = withCountry ? $"{near.Name}, {near.CountryName}" : near.Label;
        return Strings.T($"Near {name}", $"Bei {name}");
    }

    /// <summary>"5 minutes ago", "3 hours ago", "on 05.11."</summary>
    static string Ago(DateTime at)
    {
        var span = DateTime.Now - at;
        if (span.TotalMinutes < 2) return Strings.T("just now", "gerade eben");
        if (span.TotalHours < 1) return Strings.T($"{(int)span.TotalMinutes} minutes ago", $"vor {(int)span.TotalMinutes} Minuten");
        if (span.TotalHours < 24) return Strings.T($"{(int)span.TotalHours} hours ago", $"vor {(int)span.TotalHours} Stunden");
        return Strings.T($"on {at:dd.MM.}", $"am {at:dd.MM.}");
    }

    bool SetCoordinate(string text, bool latitude)
    {
        if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
        if (latitude ? Math.Abs(v) > 90 : Math.Abs(v) > 180) return false;
        Change(c =>
        {
            if (latitude) c.Location.Latitude = v;
            else c.Location.Longitude = v;
            c.Location.PlaceName = null;
        });
        return true;
    }

    static string ModeHelp(LocationSettings l) => l.Mode switch
    {
        LocationMode.Windows => Strings.T("Only the Windows location; if it does not answer, the time zone.", "Nur der Windows-Standort; antwortet er nicht, die Zeitzone."),
        LocationMode.Coordinates => Strings.T("The city or coordinates below.", "Die Stadt oder die Koordinaten unten."),
        LocationMode.TimeZone => Strings.T("The main city of your time zone. Needs nothing, but can be half an hour off.",
            "Die Hauptstadt deiner Zeitzone. Braucht nichts, kann aber eine halbe Stunde danebenliegen."),
        _ => l.Latitude != null && l.Longitude != null
            ? Strings.T("The Windows location; if Windows does not answer, the fixed place set before, then the time zone.",
                "Der Windows-Standort; antwortet Windows nicht, der früher gesetzte feste Ort, dann die Zeitzone.")
            : Strings.T("The Windows location; if Windows does not answer, the time zone.", "Der Windows-Standort; antwortet Windows nicht, die Zeitzone."),
    };

    // ---------- Settings ----------

    void BuildSettingsPage()
    {
        SettingsPanel.Children.Clear();
        var config = Engine.Settings;
        var general = Ui.Card(SettingsPanel, Strings.T("General", "Allgemein"));
        // Installed for all users, the device starts DynaTones and only an administrator changes that; with StartQueue
        // installed, StartQueue starts it in its order and the switch goes through StartQueue.
        var startToggle = Locked(Ui.Toggle(Autostart.IsOn, on => Autostart.Set(on)), Policies.StartWithWindows);
        if (Autostart.ForAllUsers)
        {
            startToggle.IsEnabled = false;
            startToggle.ToolTip = Strings.T("Installed for all users of this device: it starts for everyone, and only an administrator can change that.",
                "Für alle Benutzer dieses Geräts installiert: Es startet bei allen, und nur ein Administrator kann das ändern.");
            ToolTipService.SetShowOnDisabled(startToggle, true);
        }
        Ui.Row(general, Strings.T("Start with Windows", "Mit Windows starten"),
            Autostart.ForAllUsers ? Strings.T("For all users of this device.", "Für alle Benutzer dieses Geräts.")
            : Autostart.ManagedByStartQueue != null ? Strings.T("StartQueue starts DynaTones in its startup order.", "StartQueue startet DynaTones in seiner Startreihenfolge.")
            : Strings.T("Through your own autostart entry; no admin rights.", "Über deinen eigenen Autostart-Eintrag; ohne Adminrechte."),
            startToggle);
        Ui.Row(general, Strings.T("Color mode of this window", "Farbmodus dieses Fensters"),
            Strings.T("Only this window; the color mode of Windows is set under Color mode.",
                "Nur dieses Fenster; den Farbmodus von Windows stellst du unter Farbmodus ein."), Ui.Segments(new[]
        {
            (ThemeChoice.System, Strings.T("Like Windows", "Wie Windows")),
            (ThemeChoice.Light, Strings.T("Light", "Hell")),
            (ThemeChoice.Dark, Strings.T("Dark", "Dunkel")),
        }, config.WindowTheme, t =>
        {
            Change(c => c.WindowTheme = t);
            Theme.Apply(t, Engine.Settings.Accent);
        }));
        AccentRow.Add(general, config.Accent, profiles: true, a =>
        {
            Change(c => c.Accent = a);
            ShowProfileColor();
            Theme.Apply(Engine.Settings.WindowTheme, a);
        });
        Ui.Row(general, Strings.T("Language", "Sprache"), Strings.T("Takes effect when the window opens again.", "Gilt, wenn das Fenster neu geöffnet wird."),
            Locked(Ui.Choice(new[] { ((string?)null, Strings.T("Like Windows", "Wie Windows")), ("en", "English"), ("de", "Deutsch") },
                config.Language, v => Change(c => c.Language = v), 200), Policies.Language), showHelp: true);
        // Basic or Full, as chosen in the setup; Full later with one UAC prompt, and none after it (STANDARD "Full").
        if (!KitApp.PerMachine)
        {
            FrameworkElement setup;
            if (IsFull) setup = new TextBlock { Style = S("Value"), Text = Strings.T("Full", "Vollständig"), VerticalAlignment = VerticalAlignment.Center };
            else
            {
                var shield = new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", FontSize = 13, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                var full = new Button { Style = S("SecondaryButton"), Content = Ui.Horizontal(shield, new TextBlock { Text = Strings.T("Set up fully", "Vollständig einrichten"), VerticalAlignment = VerticalAlignment.Center }) }
                    .Tip(Strings.T("Windows asks once for your confirmation. After that DynaTones does not ask again.",
                        "Windows fragt einmal nach deiner Bestätigung. Danach fragt DynaTones nicht mehr."));
                Speak(full, Strings.T("Set up fully", "Vollständig einrichten"));
                full.Click += async (_, _) =>
                {
                    full.IsEnabled = false;
                    await Task.Run(KitSetup.SetUpFull);
                    SignIn.Recheck();
                    BuildSettingsPage();
                    BuildBackground();
                };
                setup = full;
            }
            Ui.Row(general, Strings.T("Set up", "Einrichtung"), IsFull
                ? Strings.T("Full, with admin rights: also the screen before signing in changes with the day.",
                    "Vollständig, mit Adminrechten: Auch der Bildschirm vor der Anmeldung wechselt mit dem Tag.")
                : Strings.T("Basic, without admin rights. Fully set up, the screen before signing in changes with the day too.",
                    "Basis, ohne Adminrechte. Vollständig eingerichtet wechselt auch der Bildschirm vor der Anmeldung mit dem Tag."), setup);
        }

        BuildLocationCards(SettingsPanel);
        AboutPage.Build(SettingsPanel);
        // The place chosen during the introduction shows on its step at once.
        if (Frame.IntroStepId == "place") Frame.RefreshStep();
    }

    // ---------- Saving ----------

    void Change(Action<Config> change)
    {
        ConfigStore.Update(change);
        Engine.Reload();
        Engine.Resolve("settings");
    }

    void ChangeAndRebuild(Action<Config> change, Action rebuild)
    {
        Change(change);
        Dispatcher.BeginInvoke(rebuild);
    }
}

/// <summary>Runs work that uses WPF imaging on a thread of its own, off the window's thread.</summary>
static class Sta
{
    public static Task<T> Run<T>(Func<T> work)
    {
        var done = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return done.Task;
    }
}
