using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>
/// The introduction at the first start (kit KitFrame.StartIntro, STANDARD "First start"): Welcome, Place and pictures,
/// Switch on. Until the user switches DynaTones on, the background and the color mode stay as they are; the plan is
/// shown all the same, and what is chosen here is kept.
/// </summary>
public partial class MainWindow
{
    /// <summary>Shows the introduction when DynaTones is not switched on yet.</summary>
    void StartIntro()
    {
        Frame.IntroChanged += IntroChanged;
        Frame.StartIntro(
        [
            new IntroStep("welcome", () => Strings.T("Welcome", "Willkommen"), BuildIntroWelcome),
            new IntroStep("place", () => Strings.T("Place and pictures", "Ort und Bilder"), BuildIntroPlace),
            new IntroStep("ready", () => Strings.T("Switch on", "Einschalten"), BuildIntroReady),
        ]);
        IntroChanged();
    }

    /// <summary>
    /// In the introduction, looking around or switched on: the header says what holds. Pausing has no use before
    /// anything runs, and while looking around "Switch on" is the main action.
    /// </summary>
    void IntroChanged()
    {
        UpdateAddButton();
        RefreshBackground();
    }

    /// <summary>
    /// One primary per view: Add package is the window's, except while looking around ("Switch on" is the main action
    /// then) and in the profile editor, whose Save is the view's one primary.
    /// </summary>
    void UpdateAddButton()
    {
        var editorShown = editingProfile && ProfilesView.Visibility == Visibility.Visible;
        AddButton.Style = S(Frame.LookingAround || editorShown ? "HeroButton" : "PrimaryButton");
    }

    /// <summary>For the snapshot: the step <paramref name="index"/> of the introduction, or looking around (-1).</summary>
    internal void ShowIntroForSnapshot(int index)
    {
        if (index < 0) Frame.LookAround();
        else Frame.ShowStep(index);
    }

    internal bool InIntro => KitIntro.Pending;

    /// <summary>The words of the header while not switched on.</summary>
    static string NotSwitchedOn => Strings.T("Not switched on yet. Your background stays as it is.",
        "Noch nicht eingeschaltet. Dein Hintergrund bleibt, wie er ist.");

    // ---------- Step 1: Welcome ----------

    FrameworkElement BuildIntroWelcome()
    {
        var page = new StackPanel();
        page.Children.Add(new TextBlock { Style = S("SectionLabel"), Text = Strings.T("WELCOME", "WILLKOMMEN") });
        page.Children.Add(Heading(Strings.T("Your desktop goes with the day.", "Dein Desktop geht mit dem Tag.")));
        page.Children.Add(Lead(Strings.T(
            "DynaTones changes the background, the lock screen and light and dark with the sun at your place. Gently, without you having to do anything.",
            "DynaTones wechselt Hintergrund, Sperrbildschirm sowie Hell und Dunkel mit der Sonne an deinem Ort. Sanft, ohne dass du etwas tun musst.")));
        var list = new StackPanel();
        list.Children.Add(Feature("", Strings.T("Pictures that go with the day", "Bilder, die mit dem Tag gehen"),
            Strings.T("From night through the day to night, from the gallery or from your own photos.",
                "Von der Nacht durch den Tag bis zur Nacht, aus der Galerie oder aus deinen eigenen Fotos."), true));
        list.Children.Add(Feature("", Strings.T("Light and dark at the right time", "Hell und dunkel zur richtigen Zeit"),
            Strings.T("Windows and apps turn light after sunrise and dark before sunset.",
                "Windows und Apps werden nach Sonnenaufgang hell und vor Sonnenuntergang dunkel."), true));
        list.Children.Add(Feature("", Strings.T("Everything stays in your hands", "Alles bleibt in deiner Hand"),
            Strings.T("Pause, the next picture or the other color mode, with one click, also from the tray.",
                "Pausieren, das nächste Bild oder der andere Farbmodus, mit einem Klick, auch aus dem Tray."), false));
        page.Children.Add(new Border { Style = S("Card"), Child = list, Padding = new Thickness(24, 8, 24, 8), Margin = new Thickness(0, 0, 0, 16) });
        page.Children.Add(Notice(Strings.T(
            "So far DynaTones has only looked and changed nothing. Your background and light or dark stay as they are until you switch it on.",
            "Bisher hat DynaTones nur nachgesehen und nichts geändert. Dein Hintergrund und der Farbmodus bleiben, wie sie sind, bis du es einschaltest.")));
        return page;
    }

    // ---------- Step 2: Place and pictures ----------

    FrameworkElement BuildIntroPlace()
    {
        var page = new StackPanel();
        page.Children.Add(Heading(Strings.T("Where are you, and which pictures would you like?", "Wo bist du, und welche Bilder möchtest du?")));
        page.Children.Add(Lead(Strings.T("From your place DynaTones calculates the course of the sun. Pictures and color mode go with it.",
            "Aus deinem Ort berechnet DynaTones den Lauf der Sonne. Bilder und Farbmodus gehen mit ihm.")));
        BuildLocationCards(page);

        var pictures = new StackPanel();
        pictures.Children.Add(new TextBlock { Style = S("SectionLabel"), Text = Strings.T("PICTURES FOR THE DESKTOP", "BILDER FÜR DEN DESKTOP"), Margin = new Thickness(0, 0, 0, 12) });
        var tiles = new UniformGrid { Columns = 4 };
        var chosen = Engine.Settings.Wallpaper.PackageId;
        foreach (var package in Packages.LoadAll().Where(p => !removing.Contains(p.Id) && p.Images.Count > 0))
        {
            var preview = package.PathOf(package.Images[Math.Min(package.Images.Count - 1, package.Images.Count / 2)]);
            tiles.Children.Add(PackageTile(Picture(preview, double.NaN, 64), package.DisplayName, package.Id == chosen,
                () => ChangeAndRebuild(c => c.Wallpaper.PackageId = package.Id, Frame.RefreshStep)));
        }
        tiles.Children.Add(ActionTile(Glyph(""), Strings.T("From the gallery", "Aus der Galerie"), () =>
        {
            // The gallery is a page of the window: looked at while looking around, "Continue setup" comes back here.
            Frame.LookAround();
            OpenGallery();
        }));
        Button? own = null;
        own = ActionTile(Glyph(""), Strings.T("Your own photos", "Eigene Fotos"), null);
        KitMenuButton.Attach(own, FillAddMenu);
        tiles.Children.Add(own);
        pictures.Children.Add(tiles);
        page.Children.Add(new Border { Style = S("Card"), Child = pictures, Padding = new Thickness(24, 20, 24, 20), Margin = new Thickness(0, 24, 0, 16) });

        // Light and dark: one switch, as the card's only row (no line above it).
        var mode = new Grid();
        mode.ColumnDefinitions.Add(new ColumnDefinition());
        mode.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var words = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
        var label = Strings.T("Light and dark with the day", "Hell und dunkel mit dem Tag");
        var help = Strings.T("Windows and apps turn light after sunrise and dark before sunset. The times are under Color mode.",
            "Windows und Apps werden nach Sonnenaufgang hell und vor Sonnenuntergang dunkel. Die Zeiten stehen unter Farbmodus.");
        words.Children.Add(new TextBlock { Style = S("RowLabel"), Text = label });
        words.Children.Add(new TextBlock { Style = S("Help"), Text = help, TextWrapping = TextWrapping.Wrap });
        mode.Children.Add(words);
        var toggle = Locked(Ui.Toggle(Engine.Settings.Appearance.Enabled, on => ChangeAndRebuild(c => c.Appearance.Enabled = on, Frame.RefreshStep)), Policies.ColorModeEnabled);
        toggle.VerticalAlignment = VerticalAlignment.Center;
        Speak(toggle, label);
        System.Windows.Automation.AutomationProperties.SetHelpText(toggle, help);
        Grid.SetColumn(toggle, 1);
        mode.Children.Add(toggle);
        page.Children.Add(new Border { Style = S("Card"), Child = mode, Padding = new Thickness(24, 20, 24, 20) });
        return page;
    }

    /// <summary>
    /// One package of pictures to choose, as the lock screen's choices are: a card with its preview and a radio button
    /// with its name; the whole card takes the click.
    /// </summary>
    Border PackageTile(FrameworkElement preview, string name, bool chosen, Action click)
    {
        var stack = new StackPanel();
        stack.Children.Add(preview);
        var radio = new RadioButton { Content = name, IsChecked = chosen, GroupName = "intro-pictures", Margin = new Thickness(0, 8, 0, 0), FontWeight = FontWeights.SemiBold };
        radio.Checked += (_, _) =>
        {
            if (!chosen) click();
        };
        stack.Children.Add(radio);
        var card = ChoiceCard(stack, chosen, new Thickness(0, 0, 8, 0));
        card.Padding = new Thickness(8);
        card.Cursor = System.Windows.Input.Cursors.Hand;
        card.MouseLeftButtonUp += (_, _) => radio.IsChecked = true;
        return card;
    }

    /// <summary>A way to more pictures, in the same size as a package.</summary>
    Button ActionTile(FrameworkElement preview, string name, Action? click)
    {
        var tile = new Button
        {
            Style = S("SecondaryButton"),
            Content = TileContent(preview, name),
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8),
            Height = double.NaN,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        if (click != null) tile.Click += (_, _) => click();
        Speak(tile, name);
        return tile;
    }

    FrameworkElement TileContent(FrameworkElement preview, string name)
    {
        var content = new StackPanel();
        preview.Margin = new Thickness(0, 0, 0, 8);
        content.Children.Add(preview);
        content.Children.Add(new TextBlock { Text = name, Style = S("RowLabel"), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap });
        return content;
    }

    FrameworkElement Glyph(string glyph) => new Border
    {
        Height = 64,
        CornerRadius = new CornerRadius(6),
        Background = B("Surface2"),
        Child = new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = glyph, FontSize = 20, Foreground = B("Text3"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };

    // ---------- Step 3: Switch on ----------

    FrameworkElement BuildIntroReady()
    {
        var page = new StackPanel();
        page.Children.Add(Heading(Strings.T("Ready to switch on", "Bereit zum Einschalten")));
        page.Children.Add(Lead(Strings.T("This is how your day goes on once you switch DynaTones on. You can change all of it afterwards under Background and Color mode.",
            "So geht dein Tag weiter, sobald du DynaTones einschaltest. Ändern kannst du alles danach unter Hintergrund und Farbmodus.")));

        var plan = Engine.Plan;
        var c = Engine.Settings;
        var rows = new StackPanel();
        string place = Engine.Position is { } position ? PlaceText(position) : Strings.T("No position yet", "Noch kein Standort");
        if (plan?.Today is { } day)
        {
            var rise = day.Crossing(Sun.SunriseElevation, true);
            var set = day.Crossing(Sun.SunriseElevation, false);
            place += Strings.T($", sunrise {Fmt(rise)}, sunset {Fmt(set)}", $", Sonnenaufgang {Fmt(rise)}, Sonnenuntergang {Fmt(set)}");
        }
        rows.Children.Add(Fact(Strings.T("Place", "Ort"), place, true));
        var package = Engine.Package;
        rows.Children.Add(Fact(Strings.T("Desktop", "Desktop"),
            !c.Wallpaper.Enabled ? Strings.T("Stays as it is", "Bleibt, wie er ist")
            : package == null ? Strings.T("No pictures chosen yet: it stays as it is", "Noch keine Bilder gewählt: Er bleibt, wie er ist")
            : plan?.Current is { } now ? Strings.T($"{package.DisplayName}, now picture {now.Image + 1} of {package.Images.Count}", $"{package.DisplayName}, jetzt Bild {now.Image + 1} von {package.Images.Count}")
            : package.DisplayName, true));
        rows.Children.Add(Fact(Strings.T("Lock screen", "Sperrbildschirm"),
            !c.LockScreen.Enabled ? Strings.T("Stays as it is", "Bleibt, wie er ist")
            : c.LockScreen.Source == LockScreenSource.Wallpaper ? Strings.T("Like the desktop", "Wie der Desktop")
            : Engine.LockPackage?.DisplayName ?? Strings.T("A package of its own", "Eigenes Paket"), true));
        rows.Children.Add(Fact(Strings.T("Light and dark", "Hell und dunkel"),
            !c.Appearance.Enabled || plan?.Dark == null ? Strings.T("Stays as Windows has it", "Bleibt, wie Windows es hat")
            : (plan.Dark == true ? Strings.T("Now dark", "Jetzt dunkel") : Strings.T("Now light", "Jetzt hell"))
              + (plan.NextModeChange is { } change ? Strings.T($", {(plan.Dark == true ? "light" : "dark")} from {Strings.Time(change)}", $", ab {Strings.Time(change)} {(plan.Dark == true ? "hell" : "dunkel")}") : ""), true));
        rows.Children.Add(Fact(Strings.T("Set up", "Eingerichtet"), IsFull
            ? Strings.T("With admin rights: also the screen before signing in", "Mit Adminrechten: auch der Bildschirm vor der Anmeldung")
            : Strings.T("Without admin rights", "Ohne Adminrechte"), false));

        var card = new Grid();
        card.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        card.ColumnDefinitions.Add(new ColumnDefinition());
        var shown = package != null && plan?.Current is { } current && c.Wallpaper.Enabled ? package.PathOf(package.Images[current.Image]) : null;
        var picture = Picture(shown, 200, 125);
        picture.VerticalAlignment = VerticalAlignment.Top;
        picture.Margin = new Thickness(0, 14, 24, 14);
        card.Children.Add(picture);
        Grid.SetColumn(rows, 1);
        card.Children.Add(rows);
        page.Children.Add(new Border { Style = S("Card"), Child = card, Padding = new Thickness(24, 0, 24, 0), Margin = new Thickness(0, 0, 0, 16) });
        page.Children.Add(Notice(Strings.T(
            "When you switch it on, the background moves gently to the picture for now. Pausing works at any time, also from the tray.",
            "Beim Einschalten wechselt der Hintergrund sanft zum Bild für jetzt. Pausieren geht jederzeit, auch aus dem Tray.")));
        return page;
    }

    /// <summary>The admin tier is in place: the event log and the screen before signing in (STANDARD "Full").</summary>
    static bool IsFull => KitApp.PerMachine || (KitSetup.IsFull && SignIn.IsInstalled);

    // ---------- Parts ----------

    // The page's heading in the family's h2 (20/900): heavier, not much bigger (design system, "Hierarchy from weight").
    TextBlock Heading(string text) => new()
    {
        Text = text, Style = S("H2"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8),
    };

    TextBlock Lead(string text) => new()
    {
        Text = text, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16),
    };

    FrameworkElement Feature(string glyph, string title, string text, bool line)
    {
        var row = new Grid { Margin = new Thickness(0, 12, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = glyph, FontSize = 20, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) });
        var words = new StackPanel();
        words.Children.Add(new TextBlock { Text = title, Style = S("RowLabel"), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        words.Children.Add(new TextBlock { Text = text, Style = S("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(words, 1);
        row.Children.Add(words);
        if (!line) return row;
        return new Border { Child = row, BorderBrush = B("Line"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    FrameworkElement Fact(string label, string value, bool line)
    {
        var row = new Grid { Margin = new Thickness(0, 12, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { Text = label, Style = S("Muted") });
        var text = new TextBlock { Text = value, Style = S("RowLabel") };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        if (!line) return row;
        return new Border { Child = row, BorderBrush = B("Line"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    Border Notice(string text) => new()
    {
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16, 12, 16, 12),
        Background = B("NoticeBg"),
        Child = Ui.Horizontal(
            new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", Foreground = B("NoticeText"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) },
            new TextBlock { Text = text, Foreground = B("NoticeText"), Margin = new Thickness(12, 0, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 660 }),
    };
}
