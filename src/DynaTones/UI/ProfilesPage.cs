using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>
/// The Profiles tab. Everything about profiles that every EsteemWorks app has comes from the kit (KitProfiles, design
/// system principle 8 "Same purpose, same piece"): the bar of chips with "No profile" and "+ Profile", the line under
/// it with its actions, the chips' menu (edit, use by hand, pause, precedence, remove), the editor, the pause and the
/// order as pages of this tab, and the week. DynaTones adds only its own: for the profile chosen in the bar what it
/// changes in DynaTones, each value either its own (with a reset) or "übernommen" from the settings under Background
/// and Color mode.
/// </summary>
public partial class MainWindow
{
    /// <summary>The profile chosen in the bar; null for "No profile".</summary>
    Guid? shownProfile;
    /// <summary>A profile was chosen in the bar; until then it shows the one that applies.</summary>
    bool profileChosen;
    /// <summary>The profile the kit's editor shows, whose color the window takes meanwhile.</summary>
    FamilyProfile? editedProfile;

    ProfileHost? profileHost;
    ProfileBar? profileBar;
    readonly StackPanel profileBody = new();

    bool profilesFollow;
    Guid? profilesShownActive;

    /// <summary>What DynaTones gives the kit's profile pieces: the profile shown, what a profile changes in DynaTones.</summary>
    ProfileHost ProfileHost => profileHost ??= new ProfileHost
    {
        App = Profiles.App,
        Shown = () => shownProfile,
        Show = id =>
        {
            shownProfile = id;
            profileChosen = true;
            // The kit builds its bar anew right after; building it here too took the keyboard focus off the chip.
            BuildProfilesPage(refreshBar: false);
            ShowProfileColor();
        },
        NoProfileHelp = Strings.T("Without a profile: your settings under Background and Color mode, as they are",
            "Ohne Profil: deine Einstellungen unter Hintergrund und Farbmodus, wie sie sind"),
        // What a profile changes in DynaTones lives in config.json, by the profile's id.
        Moved = MoveProfileSettings,
        Removed = id => Change(c => c.Profiles.Remove(id)),
        Editing = profile =>
        {
            editedProfile = profile;
            ShowProfileColor();
        },
        EditorPart = _ => new TextBlock
        {
            Style = S("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0),
            Text = Strings.T("What the profile changes in DynaTones you set on the Profiles page after saving.",
                "Was das Profil in DynaTones ändert, stellst du nach dem Speichern auf der Seite Profile ein."),
        },
        Confirm = (title, body, action, danger) => dialog.ConfirmAsync(title, body, action, danger: danger),
        Choose = (title, body, second, main) => dialog.ChooseAsync(title, body, second, main),
    };

    void BuildProfilesPage() => BuildProfilesPage(refreshBar: true);

    void BuildProfilesPage(bool refreshBar)
    {
        if (ProfilesPanel == null) return;
        // Changed here, in another app of the family, or the profile that applies changed: shown anew. While a page of
        // the tab is open (the editor, the pause, the order) the tab below is built anew behind it.
        if (!profilesFollow)
        {
            profilesFollow = true;
            void Again() => Dispatcher.BeginInvoke(() => BuildProfilesPage());
            // After a resolve only when another profile applies now: resolves come every minute.
            void ActiveChanged()
            {
                if (Engine.ActiveProfile?.Id != profilesShownActive) Again();
            }
            FamilyProfiles.Changed += Again;
            Engine.Updated += ActiveChanged;
            // A page of the tab opened or closed: the header's main button gives way to the page's Save.
            var content = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ScrollViewer));
            EventHandler pageChanged = (_, _) => UpdateAddButton();
            content.AddValueChanged(ProfilesView, pageChanged);
            Closed += (_, _) =>
            {
                FamilyProfiles.Changed -= Again;
                Engine.Updated -= ActiveChanged;
                content.RemoveValueChanged(ProfilesView, pageChanged);
            };
        }
        var all = Profiles.All;
        var active = Engine.ActiveProfile;
        profilesShownActive = active?.Id;
        if (!profileChosen || (shownProfile is { } chosen && all.All(p => p.Id != chosen))) shownProfile = active?.Id;

        // ---------- The kit's bar: built once, so the keyboard focus stays on a chip ----------
        if (profileBar == null)
        {
            profileBar = KitProfiles.Bar(ProfileHost);
            profileBar.Margin = new Thickness(0, 0, 0, 16);
            ProfilesPanel.Children.Add(profileBar);
            ProfilesPanel.Children.Add(profileBody);
        }
        else if (refreshBar) profileBar.Refresh();

        profileBody.Children.Clear();
        if (all.Count == 0)
        {
            var empty = Ui.Card(profileBody, Strings.T("Profiles", "Profile"));
            Ui.Note(empty, Strings.T(
                "A profile changes some of DynaTones' settings at certain times or when you choose it: its own package, transition, lock screen, color mode or pointer. Everything else stays as set under Background and Color mode. A profile can be shared with the other EsteemWorks apps, so one schedule switches them all.",
                "Ein Profil ändert zu bestimmten Zeiten oder wenn du es wählst einige Einstellungen von DynaTones: eigenes Paket, Übergang, Sperrbildschirm, Farbmodus oder Mauszeiger. Alles andere bleibt, wie unter Hintergrund und Farbmodus eingestellt. Ein Profil lässt sich mit den anderen EsteemWorks-Apps teilen, dann schaltet ein Zeitplan alle um."));
            return;
        }
        if (all.FirstOrDefault(p => p.Id == shownProfile) is { } shown) BuildProfileSettings(shown);

        // ---------- The week, the kit's ----------
        var week = Ui.Card(profileBody, ProfileTexts.Week);
        week.Children.Add(new ProfileWeek(all) { Margin = new Thickness(0, 8, 0, 8) });
        var legend = ProfileWeek.Legend(all, Strings.T("No profile: your settings as they are", "Ohne Profil: deine Einstellungen, wie sie sind"));
        legend.Margin = new Thickness(0, 0, 0, 8);
        week.Children.Add(legend);
    }

    /// <summary>DynaTones' own part: what the profile chosen in the bar changes in DynaTones.</summary>
    void BuildProfileSettings(FamilyProfile shown)
    {
        var card = new StackPanel();
        profileBody.Children.Add(new Border { Style = S("Card"), Child = card, Padding = new Thickness(24, 20, 24, 20), Margin = new Thickness(0, 0, 0, 16) });
        card.Children.Add(new TextBlock { Style = S("CardTitle"), Text = Strings.T($"What \"{shown.Name}\" changes in DynaTones", $"Was „{shown.Name}“ in DynaTones ändert"), TextWrapping = TextWrapping.Wrap });
        card.Children.Add(new TextBlock { Style = S("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 14),
            Text = FamilyProfiles.SharedWith(shown, Profiles.App) is { Count: > 0 } others
                ? Strings.T($"Name, color and times apply in {ProfileSharing.AppsShort(others)} too; what follows only here.",
                    $"Name, Farbe und Zeiten gelten auch in {ProfileSharing.AppsShort(others)}; was folgt, nur hier.")
                : Strings.T("A choice here applies only in this profile.", "Eine Auswahl hier gilt nur in diesem Profil.") });

        var settings = Engine.Settings;
        var own = settings.Profiles.GetValueOrDefault(shown.Id) ?? new ProfileSettings();
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(-8, 0, -8, 0) };
        card.Children.Add(grid);
        void Set(Action<ProfileSettings> set) => ChangeAndRebuild(c =>
        {
            var p = c.Profiles.GetValueOrDefault(shown.Id) ?? new ProfileSettings();
            set(p);
            if (p.IsEmpty) c.Profiles.Remove(shown.Id);
            else c.Profiles[shown.Id] = p;
        }, BuildProfilesPage);

        var packages = Packages.LoadAll().Where(p => p.Images.Count > 0).ToList();
        grid.Children.Add(ProfileSetting(Strings.T("Desktop package", "Desktop-Paket"), own.DesktopPackage != null, () => Set(p => p.DesktopPackage = null),
            Ui.Choice(packages.Select(p => ((string?)p.Id, p.DisplayName)), own.DesktopPackage ?? settings.Wallpaper.PackageId, v => Set(p => p.DesktopPackage = v), 220)));

        var codes = new[]
        {
            (0, Strings.T("Soft", "Weich")), (Transitions.Gentle, Strings.T("Gentle", "Sanft")),
            (10, Strings.T("Flowing, 10 min.", "Fließend, 10 Min.")), (30, Strings.T("Flowing, 30 min.", "Fließend, 30 Min.")), (60, Strings.T("Flowing, 60 min.", "Fließend, 60 Min.")),
        };
        var baseCode = Transitions.Code(settings.Wallpaper.Fade, settings.Wallpaper.BlendMinutes, settings.Wallpaper.BlendSeconds);
        grid.Children.Add(ProfileSetting(Strings.T("Transition", "Übergang"), own.Transition != null, () => Set(p => p.Transition = null),
            Ui.Choice(codes, own.Transition ?? (codes.Any(c => c.Item1 == baseCode) ? baseCode : 30), v => Set(p => p.Transition = v), 220)));

        var locks = new List<(string, string)> { ("desktop", Strings.T("Like the desktop", "Wie der Desktop")) };
        locks.AddRange(packages.Select(p => ("package:" + p.Id, Strings.T($"Own package: {p.DisplayName}", $"Eigenes Paket: {p.DisplayName}"))));
        locks.Add(("windows", Strings.T("Like Windows", "Wie Windows")));
        static string LockKey(bool enabled, LockScreenSource source, string? package) =>
            !enabled ? "windows" : source == LockScreenSource.Package && package != null ? "package:" + package : "desktop";
        var lockNow = own.Lock is { } l ? LockKey(l.Enabled, l.Source, l.PackageId) : LockKey(settings.LockScreen.Enabled, settings.LockScreen.Source, settings.LockScreen.PackageId);
        grid.Children.Add(ProfileSetting(Strings.T("Lock screen", "Sperrbildschirm"), own.Lock != null, () => Set(p => p.Lock = null),
            Ui.Choice(locks, lockNow, v => Set(p => p.Lock = v == "windows" ? new ProfileLock { Enabled = false }
                : v.StartsWith("package:") ? new ProfileLock { Source = LockScreenSource.Package, PackageId = v[8..] }
                : new ProfileLock { Source = LockScreenSource.Wallpaper }), 220)));

        grid.Children.Add(ProfileSetting(Strings.T("Before signing in", "Vor der Anmeldung"), own.SignIn != null, () => Set(p => p.SignIn = null),
            Ui.Choice(new[] { (true, Strings.T("There too", "Auch dort")), (false, Strings.T("Not there", "Nicht dort")) }, own.SignIn ?? settings.LockScreen.SignIn,
                v => Set(p => p.SignIn = v), 220)));

        var modes = new[]
        {
            (ModeTarget.Switch, Strings.T("Change with the day", "Mit dem Tag wechseln")), (ModeTarget.AlwaysLight, Strings.T("Always light", "Immer hell")),
            (ModeTarget.AlwaysDark, Strings.T("Always dark", "Immer dunkel")), (ModeTarget.Leave, Strings.T("Leave alone", "Nicht anfassen")),
        };
        grid.Children.Add(ProfileSetting(Strings.T("Color mode", "Farbmodus"), own.ColorMode != null, () => Set(p => p.ColorMode = null),
            Ui.Choice(modes, own.ColorMode ?? settings.Appearance.Apps, v => Set(p => p.ColorMode = v), 220)));

        var schemes = new List<(string?, string)> { (null, Strings.T("Leave alone", "Nicht anfassen")) };
        schemes.AddRange(Appearance.CursorSchemes().Select(s => ((string?)s, s)));
        var pointer = own.Pointer;
        var day = Ui.Choice(schemes, pointer != null ? pointer.Light : settings.Appearance.CursorLight, v => Set(p => p.Pointer = new ProfilePointer { Light = v, Dark = p.Pointer?.Dark ?? settings.Appearance.CursorDark }), 140);
        var night = Ui.Choice(schemes, pointer != null ? pointer.Dark : settings.Appearance.CursorDark, v => Set(p => p.Pointer = new ProfilePointer { Light = p.Pointer?.Light ?? settings.Appearance.CursorLight, Dark = v }), 140);
        Speak(day, Strings.T("Pointer by day", "Mauszeiger am Tag"));
        Speak(night, Strings.T("Pointer by night", "Mauszeiger in der Nacht"));
        grid.Children.Add(ProfileSetting(Strings.T("Mouse pointer", "Mauszeiger"), own.Pointer != null, () => Set(p => p.Pointer = null), Ui.Horizontal(day, Ui.Spacer(6), night)));
    }

    /// <summary>
    /// One setting of a profile: its own value with a reset next to the name, or the value that applies anyway, marked
    /// "übernommen" (taken over from Background and Color mode).
    /// </summary>
    UIElement ProfileSetting(string label, bool own, Action reset, FrameworkElement control)
    {
        var row = new Grid { Margin = new Thickness(8, 0, 8, 0), MinHeight = 52 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = Ui.Horizontal(new TextBlock { Style = S("RowLabel"), Text = label, VerticalAlignment = VerticalAlignment.Center });
        name.VerticalAlignment = VerticalAlignment.Center;
        if (own)
        {
            var back = new Button { Style = S("IconButton"), Width = 26, Height = 26, Margin = new Thickness(6, 0, 0, 0),
                Content = new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", FontSize = 12 } };
            back.ToolTip = Strings.T("Back to the value taken over from Background and Color mode", "Zurück zum übernommenen Wert aus Hintergrund und Farbmodus");
            Speak(back, Strings.T($"Reset {label}", $"{label} zurücksetzen"));
            back.Click += (_, _) => reset();
            name.Children.Add(back);
        }
        else
        {
            var tag = new Border { Style = S("Chip"), Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock { Style = S("ChipLabel"), Text = Strings.T("taken over", "übernommen") } };
            tag.ToolTip = Strings.T("Taken over from your settings under Background and Color mode. A choice here applies only in this profile.",
                "Aus deinen Einstellungen unter Hintergrund und Farbmodus übernommen. Eine Auswahl gilt nur in diesem Profil.");
            name.Children.Add(tag);
            control.Opacity = 0.7;
        }
        row.Children.Add(name);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        var stack = new StackPanel();
        stack.Children.Add(new Border { Style = S("Divider"), Margin = new Thickness(8, 0, 8, 0) });
        stack.Children.Add(row);
        return stack;
    }

    /// <summary>Package, transition, lock screen, color mode and pointer of a profile, from one id to another.</summary>
    void MoveProfileSettings(Guid from, Guid to)
    {
        if (from == to) return;
        Change(c =>
        {
            if (!c.Profiles.Remove(from, out var settings)) return;
            if (!c.Profiles.ContainsKey(to)) c.Profiles[to] = settings;
        });
    }

    /// <summary>A page of the Profiles tab is open (the kit's editor, pause or order).</summary>
    bool ProfilePageOpen => ProfilesView.Visibility == Visibility.Visible && KitSubpage.Depth(ProfilesView) > 0;

    /// <summary>For the snapshot: a page of the Profiles tab ("editor" for a new profile, "pause", "order"), or back.</summary>
    internal void ShowProfilePageForSnapshot(string? page)
    {
        TabProfiles.IsChecked = true;
        ShowPage();
        if (page == null)
        {
            while (KitSubpage.Depth(ProfilesView) > 0) KitSubpage.Back(ProfilesView, focus: false);
            return;
        }
        var first = Profiles.All.FirstOrDefault();
        if (profileBar == null) return;
        switch (page)
        {
            case "editor":
                KitProfiles.OpenEditor(profileBar, ProfileHost, null);
                break;
            case "pause" when first != null:
                KitProfiles.OpenPause(profileBar, ProfileHost, first.Id);
                break;
            case "order":
                KitProfiles.OpenOrder(profileBar, ProfileHost);
                break;
        }
    }

    /// <summary>A note on the pages under Background and Color mode while a profile changes what they set.</summary>
    UIElement? ProfileNote()
    {
        if (Engine.ActiveProfile is not { } active || !Engine.Settings.Profiles.TryGetValue(active.Id, out var p) || p.IsEmpty) return null;
        return Warning(Strings.T($"The profile \"{active.Name}\" applies now and changes some of what is set here. Here you set what applies without a profile.",
            $"Gerade gilt das Profil „{active.Name}“ und ändert einiges von dem, was hier eingestellt ist. Hier stellst du ein, was ohne Profil gilt."));
    }
}
