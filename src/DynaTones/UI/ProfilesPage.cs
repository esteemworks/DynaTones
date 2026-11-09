using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>
/// The Profiles tab, as designed: the profiles as chips with "+ Profile" (new, or one another EsteemWorks app offers),
/// which one applies now; for the chosen one what it changes in DynaTones, each value either its own (with a reset)
/// or "übernommen" from the settings under Background and Color mode; "For all EsteemWorks apps"; and the week, which
/// profile applies when. Name, color and schedule are edited on a page of their own with the family's shared fields.
/// </summary>
public partial class MainWindow
{
    Guid? shownProfile;
    bool editingProfile;
    bool newProfile;
    readonly ProfileFields profileFields = new(Profiles.App);

    bool profilesFollow;
    Guid? profilesShownActive;

    void BuildProfilesPage()
    {
        if (ProfilesPanel == null) return;
        // Changed here, in another app of the family, or the profile that applies changed: shown anew, but never
        // while the editor is open.
        if (!profilesFollow)
        {
            profilesFollow = true;
            void Again()
            {
                if (!editingProfile && ProfilesView.Visibility == Visibility.Visible) Dispatcher.BeginInvoke(BuildProfilesPage);
            }
            // After a resolve only when another profile applies now: resolves come every minute.
            void ActiveChanged()
            {
                if (Engine.ActiveProfile?.Id != profilesShownActive) Again();
            }
            FamilyProfiles.Changed += Again;
            Engine.Updated += ActiveChanged;
            Closed += (_, _) =>
            {
                FamilyProfiles.Changed -= Again;
                Engine.Updated -= ActiveChanged;
            };
        }
        ProfilesPanel.Children.Clear();
        if (editingProfile)
        {
            BuildProfileEditor();
            return;
        }
        var all = Profiles.All;
        var active = Engine.ActiveProfile;
        profilesShownActive = active?.Id;
        if (shownProfile == null || all.All(p => p.Id != shownProfile)) shownProfile = active?.Id ?? all.FirstOrDefault()?.Id;

        // ---------- The profiles as chips ----------
        var bar = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var chips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        foreach (var profile in all)
        {
            var color = FamilyProfiles.ColorOf(FamilyProfiles.Current.Profiles, profile);
            var chip = new RadioButton { Style = S("ProfileChip"), GroupName = "profiles", IsChecked = profile.Id == shownProfile, Margin = new Thickness(0, 0, 6, 6),
                Content = Ui.Horizontal(new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Hex(color.Mid), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = profile.Name, VerticalAlignment = VerticalAlignment.Center }) };
            var id = profile.Id;
            chip.Checked += (_, _) =>
            {
                if (shownProfile == id) return;
                shownProfile = id;
                Dispatcher.BeginInvoke(BuildProfilesPage);
            };
            Speak(chip, profile.Name);
            chips.Children.Add(chip);
        }
        var add = Ui.Button(Strings.T("+ Profile", "+ Profil"), (s, _) => ShowProfileAddMenu((FrameworkElement)s));
        add.Margin = new Thickness(0, 0, 6, 6);
        chips.Children.Add(add);
        bar.Children.Add(chips);
        var now = new TextBlock { Style = S("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 6),
            Text = active == null ? Strings.T("No profile applies now", "Gerade gilt kein Profil")
                : FamilyProfiles.ManualFor(Profiles.App, DateTime.Now) != null ? Strings.T($"Active now: {active.Name}, chosen by hand", $"Jetzt aktiv: {active.Name}, von Hand gewählt")
                : Strings.T($"Active now: {active.Name}, by schedule", $"Jetzt aktiv: {active.Name}, nach Zeitplan") };
        Grid.SetColumn(now, 1);
        bar.Children.Add(now);
        ProfilesPanel.Children.Add(bar);

        if (all.Count == 0)
        {
            var empty = Ui.Card(ProfilesPanel, Strings.T("Profiles", "Profile"));
            Ui.Note(empty, Strings.T(
                "A profile changes some of DynaTones' settings at certain times or when you choose it: its own package, transition, lock screen, color mode or pointer. Everything else stays as set under Background and Color mode. A profile can be shared with StartQueue and the other EsteemWorks apps, so one schedule switches them all.",
                "Ein Profil ändert zu bestimmten Zeiten oder wenn du es wählst einige Einstellungen von DynaTones: eigenes Paket, Übergang, Sperrbildschirm, Farbmodus oder Mauszeiger. Alles andere bleibt, wie unter Hintergrund und Farbmodus eingestellt. Ein Profil lässt sich mit StartQueue und den anderen EsteemWorks-Apps teilen, dann schaltet ein Zeitplan alle um."));
            var first = Ui.Button(Strings.T("New profile", "Neues Profil"), (s, _) => ShowProfileAddMenu((FrameworkElement)s), "PrimaryButton");
            first.HorizontalAlignment = HorizontalAlignment.Left;
            first.Margin = new Thickness(0, 12, 0, 8);
            empty.Children.Add(first);
            return;
        }
        if (all.FirstOrDefault(p => p.Id == shownProfile) is not { } shown) return;

        // ---------- The chosen profile ----------
        var card = new StackPanel();
        ProfilesPanel.Children.Add(new Border { Style = S("Card"), Child = card, Padding = new Thickness(24, 18, 24, 18), Margin = new Thickness(0, 0, 0, 16) });
        var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Style = S("H2"), Text = shown.Name });
        var when = Ui.Horizontal(new TextBlock { Style = S("Muted"), Text = FamilyProfiles.Describe(all, shown, DateTime.Now), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        var change = Ui.Button(Strings.T("Change", "Ändern"), (_, _) => EditProfile(shown.Id), "GhostButton")
            .Tip(Strings.T("Name, color, days and hours, for every app that uses the profile.", "Name, Farbe, Tage und Uhrzeiten, für alle Apps, die das Profil nutzen."));
        change.Margin = new Thickness(6, 0, 0, 0);
        when.Children.Add(change);
        title.Children.Add(when);
        head.Children.Add(title);
        var manual = FamilyProfiles.ManualFor(Profiles.App, DateTime.Now);
        var switchButton = manual?.Profile == shown.Id
            ? Ui.Button(Strings.T("Back to the schedule", "Zurück zum Zeitplan"), (_, _) => FamilyProfiles.ClearManual())
            : Ui.Button(Strings.T("Use now", "Jetzt verwenden"), (_, _) => FamilyProfiles.SetManual(shown.Id, null))
                .Tip(Strings.T("Applies this profile until you switch back, in every app that uses it.", "Verwendet dieses Profil, bis du zurückschaltest, in allen Apps, die es nutzen."));
        switchButton.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(switchButton, 1);
        head.Children.Add(switchButton);
        card.Children.Add(head);

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

        var share = new CheckBox { IsChecked = shown.Shared, Margin = new Thickness(0, 16, 0, 0),
            Content = Ui.Horizontal(new TextBlock { Text = Strings.T("Offer to all EsteemWorks apps", "Für alle EsteemWorks-Apps bereitstellen") }, new TextBlock { Style = S("InfoGlyph") }) };
        share.ToolTip = Strings.T("Other EsteemWorks apps can choose this profile when they add one and attach their own settings to it. Name and schedule are then the same everywhere.",
            "Andere EsteemWorks-Apps können dieses Profil beim Anlegen auswählen und ihre eigenen Einstellungen daran hängen. Name und Zeitplan gelten dann überall gleich.");
        share.Click += (_, _) => FamilyProfiles.Change(shown.Id, p => p.Shared = share.IsChecked == true);
        card.Children.Add(share);
        if (FamilyProfiles.SharedWith(shown, Profiles.App) is { Count: > 0 } others)
            card.Children.Add(new TextBlock { Style = S("Muted"), Margin = new Thickness(0, 6, 0, 0),
                Text = Strings.T($"Also used by {string.Join(", ", others)}.", $"Auch genutzt von {string.Join(", ", others)}.") });

        // ---------- The week ----------
        var week = Ui.Card(ProfilesPanel, Strings.T("The week", "Die Woche"));
        week.Children.Add(new WeekView(all) { Margin = new Thickness(0, 8, 0, 8) });
        var legend = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var profile in all)
        {
            var color = FamilyProfiles.ColorOf(FamilyProfiles.Current.Profiles, profile);
            legend.Children.Add(Ui.Horizontal(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Background = Hex(color.Mid), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Style = S("Muted"), Text = profile.Name, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center }));
        }
        legend.Children.Add(Ui.Horizontal(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Background = B("Surface2"), BorderBrush = B("Line"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Style = S("Muted"), Text = Strings.T("No profile: your settings as they are", "Kein Profil: deine Einstellungen, wie sie sind"), VerticalAlignment = VerticalAlignment.Center }));
        week.Children.Add(legend);
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
            var tag = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 2), Margin = new Thickness(8, 0, 0, 0), Background = B("ChipBg"), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Strings.T("taken over", "übernommen"), FontSize = 11, Foreground = B("ChipText") } };
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

    void ShowProfileAddMenu(FrameworkElement owner)
    {
        var items = new List<(string, Action)> { (Strings.T("New profile", "Neues Profil"), () => EditProfile(null)) };
        foreach (var offered in FamilyProfiles.Offered(Profiles.App))
        {
            var from = string.Join(", ", offered.Apps);
            items.Add((Strings.T($"{offered.Name} (from {from})", $"{offered.Name} (aus {from})"), () => TakeOverProfile(offered.Id)));
        }
        ShowMenu(owner, items.ToArray());
    }

    void TakeOverProfile(Guid id)
    {
        FamilyProfiles.TakeOver(id, Profiles.App);
        shownProfile = id;
        Log.Info("Took over a profile another app offered");
        BuildProfilesPage();
    }

    void EditProfile(Guid? id)
    {
        editingProfile = true;
        newProfile = id == null;
        if (id != null) shownProfile = id;
        ShowProfileColor();
        BuildProfilesPage();
        ProfilesView?.ScrollToHome();
    }

    /// <summary>Name, color, days, hours and sharing: the family's shared fields, with Save, as in StartQueue.</summary>
    void BuildProfileEditor()
    {
        var head = Ui.Horizontal();
        var back = new Button { Style = S("SecondaryButton"), Content = Ui.Horizontal(
            new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = Strings.T("Back", "Zurück"), Margin = new Thickness(8, 0, 0, 0) }) };
        back.Click += (_, _) => CloseProfileEditor();
        Speak(back, Strings.T("Back", "Zurück"));
        head.Children.Add(back);
        head.Children.Add(new TextBlock { Style = S("H2"), Text = newProfile ? Strings.T("New profile", "Neues Profil") : Strings.T("Change profile", "Profil ändern"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) });
        head.Margin = new Thickness(0, 0, 0, 14);
        ProfilesPanel.Children.Add(head);

        var card = new StackPanel();
        ProfilesPanel.Children.Add(new Border { Style = S("Card"), Child = card, Padding = new Thickness(24, 18, 24, 18) });
        if (profileFields.Parent is Panel old) old.Children.Remove(profileFields);
        var editing = newProfile ? null : FamilyProfiles.Find(shownProfile ?? Guid.Empty);
        profileFields.Load(editing, Profiles.All);
        card.Children.Add(profileFields);
        profileFields.AppPart.Children.Clear();
        profileFields.AppPart.Children.Add(new TextBlock { Style = S("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0),
            Text = Strings.T("What the profile changes in DynaTones is set on the Profiles page after saving.", "Was das Profil in DynaTones ändert, stellst du nach dem Speichern auf der Seite Profile ein.") });

        var buttons = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        if (editing != null)
        {
            var delete = Ui.Button(Strings.T("Delete profile", "Profil löschen"), async (_, _) =>
            {
                var others = FamilyProfiles.SharedWith(editing, Profiles.App);
                var ok = await dialog.ConfirmAsync(Strings.T($"Delete \"{editing.Name}\"?", $"„{editing.Name}“ löschen?"),
                    others.Count > 0
                        ? Strings.T($"DynaTones stops using it; {string.Join(", ", others)} keeps it.", $"DynaTones nutzt es nicht mehr; {string.Join(", ", others)} behält es.")
                        : Strings.T("The profile and what it changes in DynaTones are gone.", "Das Profil und was es in DynaTones ändert, sind weg."),
                    Strings.T("Delete", "Löschen"), danger: true);
                if (!ok) return;
                if (others.Count > 0) FamilyProfiles.Leave(editing.Id, Profiles.App);
                else FamilyProfiles.Delete(editing.Id);
                Change(c => c.Profiles.Remove(editing.Id));
                shownProfile = null;
                CloseProfileEditor();
            }, "GhostButton");
            delete.HorizontalAlignment = HorizontalAlignment.Left;
            buttons.Children.Add(delete);
        }
        var right = Ui.Horizontal(Ui.Button(Strings.T("Cancel", "Abbrechen"), (_, _) => CloseProfileEditor()), Ui.Spacer(),
            Ui.Button(Strings.T("Save", "Speichern"), (_, _) => SaveProfile(), "PrimaryButton"));
        right.HorizontalAlignment = HorizontalAlignment.Right;
        buttons.Children.Add(right);
        card.Children.Add(buttons);
        Dispatcher.BeginInvoke(profileFields.FocusName);
    }

    void SaveProfile()
    {
        if (profileFields.Read() is not { } profile) return;
        if (newProfile)
        {
            if (profileFields.TakesOver is { } id)
            {
                FamilyProfiles.TakeOver(id, Profiles.App);
                FamilyProfiles.Save(profile);
            }
            else FamilyProfiles.Add(profile, Profiles.App);
            Log.Info($"Added profile \"{profile.Name}\"");
        }
        else FamilyProfiles.Save(profile);
        shownProfile = profile.Id;
        CloseProfileEditor();
    }

    /// <summary>For the snapshot: the editor for a new profile, then back.</summary>
    internal void ShowProfileEditorForSnapshot(bool open)
    {
        TabProfiles.IsChecked = true;
        ShowPage();
        if (open) EditProfile(null);
        else CloseProfileEditor();
    }

    void CloseProfileEditor()
    {
        editingProfile = false;
        ShowProfileColor();
        BuildProfilesPage();
    }

    /// <summary>A note on the pages under Background and Color mode while a profile changes what they set.</summary>
    UIElement? ProfileNote()
    {
        if (Engine.ActiveProfile is not { } active || !Engine.Settings.Profiles.TryGetValue(active.Id, out var p) || p.IsEmpty) return null;
        return Warning(Strings.T($"The profile \"{active.Name}\" applies now and changes some of what is set here. Here you set what applies without a profile.",
            $"Gerade gilt das Profil „{active.Name}“ und ändert einiges von dem, was hier eingestellt ist. Hier stellst du ein, was ohne Profil gilt."));
    }
}

/// <summary>The week as seven rows from 0 to 24, colored by the profile that applies, gray where none does.</summary>
sealed class WeekView : FrameworkElement
{
    readonly IReadOnlyList<FamilyProfile> profiles;
    const double LabelWidth = 36, RowHeight = 20, Gap = 6, Axis = 20;

    public WeekView(IReadOnlyList<FamilyProfile> profiles)
    {
        this.profiles = profiles;
        Height = 7 * (RowHeight + Gap) + Axis;
        Theme.Changed += InvalidateVisual;
        Unloaded += (_, _) => Theme.Changed -= InvalidateVisual;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth - LabelWidth;
        if (width <= 0) return;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var muted = Theme.Brush("Text3");
        var none = Theme.Brush("Surface2");
        // A Monday in the coming week as the reference, so pauses that are over do not show.
        var monday = DateTime.Today.AddDays(((int)DayOfWeek.Monday - (int)DateTime.Today.DayOfWeek + 7) % 7);
        var row = 0;
        foreach (var day in FamilyProfiles.WeekFromMonday)
        {
            var date = monday.AddDays(((int)day - (int)DayOfWeek.Monday + 7) % 7);
            var top = row * (RowHeight + Gap);
            var label = new FormattedText(FamilyProfiles.DayShort(day), Strings.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, muted, dpi);
            dc.DrawText(label, new Point(0, top + (RowHeight - label.Height) / 2));
            dc.PushClip(new RectangleGeometry(new Rect(LabelWidth, top, width, RowHeight), 4, 4));
            dc.DrawRectangle(none, null, new Rect(LabelWidth, top, width, RowHeight));
            // Runs of the same profile as one block each, so no seams show between the quarters of an hour.
            FamilyProfile? run = null;
            var runStart = 0;
            for (var slot = 0; slot <= 96; slot++)
            {
                var profile = slot < 96 ? FamilyProfiles.Scheduled(profiles, date.AddMinutes(slot * 15 + 7)) : null;
                if (slot < 96 && profile?.Id == run?.Id) continue;
                if (run != null)
                {
                    var color = FamilyProfiles.ColorOf(FamilyProfiles.Current.Profiles, run);
                    var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color.Mid)!;
                    dc.DrawRectangle(brush, null, new Rect(LabelWidth + runStart * width / 96, top, (slot - runStart) * width / 96, RowHeight));
                }
                run = profile;
                runStart = slot;
            }
            dc.Pop();
            row++;
        }
        foreach (var h in new[] { 0, 6, 12, 18, 24 })
        {
            var text = new FormattedText($"{h:00}:00", Strings.Culture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, muted, dpi);
            var x = LabelWidth + h * width / 24;
            dc.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, LabelWidth, ActualWidth - text.Width), 7 * (RowHeight + Gap) + 2));
        }
    }
}
