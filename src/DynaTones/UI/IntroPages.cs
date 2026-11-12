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
    /// then) and on a page of the Profiles tab (editor, pause), whose Save is the view's one primary.
    /// </summary>
    void UpdateAddButton()
    {
        AddButton.Style = S(Frame.LookingAround || ProfilePageOpen ? "HeroButton" : "PrimaryButton");
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
        page.Children.Add(IntroParts.Label(Strings.T("WELCOME", "WILLKOMMEN")));
        page.Children.Add(IntroParts.Heading(Strings.T("Your desktop goes with the day.", "Dein Desktop geht mit dem Tag.")));
        page.Children.Add(IntroParts.Lead(Strings.T(
            "DynaTones changes the background, the lock screen and light and dark with the sun at your place. Gently, without you having to do anything.",
            "DynaTones wechselt Hintergrund, Sperrbildschirm sowie Hell und Dunkel mit der Sonne an deinem Ort. Sanft, ohne dass du etwas tun musst.")));
        page.Children.Add(IntroParts.Features(
            ("", Strings.T("Pictures that go with the day", "Bilder, die mit dem Tag gehen"),
            Strings.T("From night through the day to night, from the gallery or from your own photos.",
                "Von der Nacht durch den Tag bis zur Nacht, aus der Galerie oder aus deinen eigenen Fotos.")),
            ("", Strings.T("Light and dark at the right time", "Hell und dunkel zur richtigen Zeit"),
            Strings.T("Windows and apps turn light after sunrise and dark before sunset.",
                "Windows und Apps werden nach Sonnenaufgang hell und vor Sonnenuntergang dunkel.")),
            ("", Strings.T("Everything stays in your hands", "Alles bleibt in deiner Hand"),
            Strings.T("Pause, the next picture or the other color mode, with one click, also from the tray.",
                "Pausieren, das nächste Bild oder der andere Farbmodus, mit einem Klick, auch aus dem Tray."))));
        page.Children.Add(IntroParts.Notice(Strings.T(
            "So far DynaTones has only looked and changed nothing. Your background and light or dark stay as they are until you switch it on.",
            "Bisher hat DynaTones nur nachgesehen und nichts geändert. Dein Hintergrund und der Farbmodus bleiben, wie sie sind, bis du es einschaltest.")));
        return page;
    }

    // ---------- Step 2: Place and pictures ----------

    FrameworkElement BuildIntroPlace()
    {
        var page = new StackPanel();
        page.Children.Add(IntroParts.Heading(Strings.T("Where are you, and which pictures would you like?", "Wo bist du, und welche Bilder möchtest du?")));
        page.Children.Add(IntroParts.Lead(Strings.T("From your place DynaTones calculates the course of the sun. Pictures and color mode go with it.",
            "Aus deinem Ort berechnet DynaTones den Lauf der Sonne. Bilder und Farbmodus gehen mit ihm.")));
        BuildLocationCards(page);

        var pictures = new StackPanel();
        pictures.Children.Add(new TextBlock { Style = S("SectionLabel"), Text = Strings.T("PICTURES FOR THE DESKTOP", "BILDER FÜR DEN DESKTOP"), Margin = new Thickness(0, 0, 0, 12) });
        var chosen = Engine.Settings.Wallpaper.PackageId;
        var choices = Packages.LoadAll().Where(p => !removing.Contains(p.Id) && p.Images.Count > 0).Select(package =>
            new ChoiceCardItem<string?>(package.Id, package.DisplayName, Picture(package.PathOf(package.Images[Math.Min(package.Images.Count - 1, package.Images.Count / 2)]), double.NaN, 64)));
        // The kit's choice cards (Ui.ChoiceCards): one Tab stop, the arrow keys choose.
        var tiles = Ui.ChoiceCards(choices, chosen, id => ChangeAndRebuild(c => c.Wallpaper.PackageId = id, Frame.RefreshStep), columns: 4, groupName: "intro-pictures");
        pictures.Children.Add(tiles);
        var more = new UniformGrid { Columns = 4, Margin = new Thickness(0, 8, 0, 0) };
        more.Children.Add(ActionTile(Glyph(""), Strings.T("From the gallery", "Aus der Galerie"), () =>
        {
            // The gallery is a page of the window: looked at while looking around, "Continue setup" comes back here.
            Frame.LookAround();
            OpenGallery();
        }));
        var own = ActionTile(Glyph(""), Strings.T("Your own photos", "Eigene Fotos"), null);
        KitMenuButton.Attach(own, FillAddMenu);
        more.Children.Add(own);
        pictures.Children.Add(more);
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
        Ui.Speak(toggle, label);
        System.Windows.Automation.AutomationProperties.SetHelpText(toggle, help);
        Grid.SetColumn(toggle, 1);
        mode.Children.Add(toggle);
        page.Children.Add(new Border { Style = S("Card"), Child = mode, Padding = new Thickness(24, 20, 24, 20) });
        return page;
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
        Ui.Speak(tile, name);
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
        CornerRadius = (CornerRadius)FindResource("RadiusControl"),
        Background = B("Surface2"),
        Child = new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = glyph, FontSize = 20, Foreground = B("Text3"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };

    // ---------- Step 3: Switch on ----------

    FrameworkElement BuildIntroReady()
    {
        var page = new StackPanel();
        page.Children.Add(IntroParts.Heading(Strings.T("Ready to switch on", "Bereit zum Einschalten")));
        page.Children.Add(IntroParts.Lead(Strings.T("This is how your day goes on once you switch DynaTones on. You can change all of it afterwards under Background and Color mode.",
            "So geht dein Tag weiter, sobald du DynaTones einschaltest. Ändern kannst du alles danach unter Hintergrund und Farbmodus.")));

        var plan = Engine.Plan;
        var c = Engine.Settings;
        var facts = new List<(string Label, object Value)>();
        string place = Engine.Position is { } position ? PlaceText(position) : Strings.T("No position yet", "Noch kein Standort");
        if (plan?.Today is { } day)
        {
            var rise = day.Crossing(Sun.SunriseElevation, true);
            var set = day.Crossing(Sun.SunriseElevation, false);
            place += Strings.T($", sunrise {Fmt(rise)}, sunset {Fmt(set)}", $", Sonnenaufgang {Fmt(rise)}, Sonnenuntergang {Fmt(set)}");
        }
        facts.Add((Strings.T("Place", "Ort"), place));
        var package = Engine.Package;
        facts.Add((Strings.T("Desktop", "Desktop"),
            !c.Wallpaper.Enabled ? Strings.T("Stays as it is", "Bleibt, wie er ist")
            : package == null ? Strings.T("No pictures chosen yet: it stays as it is", "Noch keine Bilder gewählt: Er bleibt, wie er ist")
            : plan?.Current is { } now ? Strings.T($"{package.DisplayName}, now picture {now.Image + 1} of {package.Images.Count}", $"{package.DisplayName}, jetzt Bild {now.Image + 1} von {package.Images.Count}")
            : package.DisplayName));
        facts.Add((Strings.T("Lock screen", "Sperrbildschirm"),
            !c.LockScreen.Enabled ? Strings.T("Stays as it is", "Bleibt, wie er ist")
            : c.LockScreen.Source == LockScreenSource.Wallpaper ? Strings.T("Like the desktop", "Wie der Desktop")
            : Engine.LockPackage?.DisplayName ?? Strings.T("A package of its own", "Eigenes Paket")));
        facts.Add((Strings.T("Light and dark", "Hell und dunkel"),
            !c.Appearance.Enabled || plan?.Dark == null ? Strings.T("Stays as Windows has it", "Bleibt, wie Windows es hat")
            : (plan.Dark == true ? Strings.T("Now dark", "Jetzt dunkel") : Strings.T("Now light", "Jetzt hell"))
              + (plan.NextModeChange is { } change ? Strings.T($", {(plan.Dark == true ? "light" : "dark")} from {Strings.Time(change)}", $", ab {Strings.Time(change)} {(plan.Dark == true ? "hell" : "dunkel")}") : "")));
        facts.Add((Strings.T("Set up", "Eingerichtet"), IsFull
            ? Strings.T("With admin rights: also the screen before signing in", "Mit Adminrechten: auch der Bildschirm vor der Anmeldung")
            : Strings.T("Without admin rights", "Ohne Adminrechte")));

        // The picture of now beside the facts, the kit's card of facts.
        var shown = package != null && plan?.Current is { } current && c.Wallpaper.Enabled ? package.PathOf(package.Images[current.Image]) : null;
        page.Children.Add(IntroParts.Facts(facts, Picture(shown, 200, 125)));
        page.Children.Add(IntroParts.Notice(Strings.T(
            "When you switch it on, the background moves gently to the picture for now. Pausing works at any time, also from the tray.",
            "Beim Einschalten wechselt der Hintergrund sanft zum Bild für jetzt. Pausieren geht jederzeit, auch aus dem Tray.")));
        return page;
    }

    /// <summary>The admin tier is in place: the event log and the screen before signing in (STANDARD "Full").</summary>
    static bool IsFull => KitApp.PerMachine || (KitSetup.IsFull && SignIn.IsInstalled);
}
