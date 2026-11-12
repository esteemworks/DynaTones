using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>
/// "Edit package", a page of the Background tab, as designed: on the left the package (preview, name, description, its
/// own transition, more details, use, export and delete), on the right its pictures in the order of the day, to drag
/// into another order, add to and remove from. Everything is saved at once; there is no Save or Cancel.
/// </summary>
public partial class MainWindow
{
    const string ImageDragFormat = "DynaTones.PackageImage";

    void BuildPackagePage(Package p)
    {
        var c = Engine.Settings;
        var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var back = BackRow(p.DisplayName);
        BackgroundPanel.Children.Remove(back);
        back.Margin = new Thickness(0);
        head.Children.Add(back);
        var saved = new TextBlock { Style = S("Muted"), Text = Strings.T("Changes are saved at once", "Änderungen werden sofort gespeichert"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(saved, 1);
        head.Children.Add(saved);
        BackgroundPanel.Children.Add(head);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        BackgroundPanel.Children.Add(columns);

        // ---------- Left: the package ----------
        var left = new StackPanel();
        columns.Children.Add(new Border { Style = S("Card"), Child = left, Padding = new Thickness(20, 20, 20, 20), VerticalAlignment = VerticalAlignment.Top });
        left.Children.Add(p.Images.Count > 0 ? DayStrip(p, 110, 640)
            : new Border { Height = 110, CornerRadius = new CornerRadius(6), Background = B("Surface2"),
                Child = new TextBlock { Style = S("Muted"), Text = Strings.T("No pictures yet", "Noch keine Bilder"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });

        TextBlock Label(string text) => new() { Style = S("Muted"), Text = text, Margin = new Thickness(0, 14, 0, 4) };
        var german = Strings.German;
        left.Children.Add(Label(Strings.T("Name", "Name")));
        var name = new TextBox { Text = p.DisplayName, HorizontalAlignment = HorizontalAlignment.Stretch }
            .Tip(Strings.T("Enter or leaving the field saves it.", "Enter oder das Verlassen des Felds speichert."));
        void SaveName()
        {
            var text = name.Text.Trim();
            if (text.Length == 0 || text == p.DisplayName) return;
            if (german && !string.IsNullOrEmpty(p.NameDe)) p.NameDe = text;
            else p.Name = text;
            SavePackage(p);
        }
        name.LostFocus += (_, _) => SaveName();
        name.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            SaveName();
        };
        Speak(name, Strings.T("Name", "Name"));
        left.Children.Add(name);

        left.Children.Add(Label(Strings.T("Description", "Beschreibung")));
        var description = new TextBox { Text = p.DescriptionShown ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        description.LostFocus += (_, _) =>
        {
            var text = description.Text.Trim();
            if (text == (p.DescriptionShown ?? "")) return;
            if (german && !string.IsNullOrEmpty(p.DescriptionDe)) p.DescriptionDe = text;
            else p.Description = text.Length == 0 ? null : text;
            Packages.Save(p);
        };
        Speak(description, Strings.T("Description", "Beschreibung"));
        left.Children.Add(description);

        // Its own transition, or the one set for the desktop.
        left.Children.Add(Label(Strings.T("Transition", "Übergang")));
        var codes = new List<(int?, string)>
        {
            (null, Strings.T("As set for the desktop", "Wie beim Desktop eingestellt")),
            (0, Strings.T("Soft, about 1 second", "Weich, etwa 1 Sekunde")),
            (Transitions.Gentle, Strings.T("Gentle, a few seconds", "Sanft, einige Sekunden")),
            (10, Strings.T("Flowing, 10 minutes", "Fließend, 10 Minuten")),
            (30, Strings.T("Flowing, 30 minutes", "Fließend, 30 Minuten")),
            (60, Strings.T("Flowing, 60 minutes", "Fließend, 60 Minuten")),
        };
        var own = p.Transition is { } t ? Transitions.Code(t.Fade, t.BlendMinutes, t.BlendSeconds) : (int?)null;
        var transition = Fill(Ui.Choice(codes, own, v =>
        {
            p.Transition = v is { } code ? Transitions.FromCode(code) : null;
            SavePackage(p);
        }, 200));
        Speak(transition, Strings.T("Transition in this package", "Übergang in diesem Paket"));
        left.Children.Add(transition);

        // More details: the other language, where the pictures come from, made with AI.
        var more = new StackPanel();
        var expander = new Expander { Header = Strings.T("More details", "Weitere Angaben"), Margin = new Thickness(0, 14, 0, 0), Content = more };
        left.Children.Add(expander);
        TextBox Detail(string label, string? value, Action<string?> set)
        {
            more.Children.Add(Label(label));
            var box = new TextBox { Text = value ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
            box.LostFocus += (_, _) =>
            {
                var text = box.Text.Trim();
                if (text == (value ?? "")) return;
                value = text;
                set(text.Length == 0 ? null : text);
                Packages.Save(p);
            };
            Speak(box, label);
            more.Children.Add(box);
            return box;
        }
        Detail(Strings.T("Name in English", "Name auf Englisch"), p.Name, v => p.Name = v ?? p.Name);
        Detail(Strings.T("Name in German", "Name auf Deutsch"), p.NameDe, v => p.NameDe = v);
        Detail(Strings.T("Description in English", "Beschreibung auf Englisch"), p.Description, v => p.Description = v);
        Detail(Strings.T("Description in German", "Beschreibung auf Deutsch"), p.DescriptionDe, v => p.DescriptionDe = v);
        Detail(Strings.T("Source", "Herkunft"), p.CreditsShown, v =>
        {
            if (german) p.CreditsDe = v;
            else p.Credits = v;
        });
        var ai = new CheckBox { Content = Strings.T("Made with AI", "Mit KI erstellt"), IsChecked = p.AiMade, Margin = new Thickness(0, 12, 0, 0) };
        ai.Click += (_, _) =>
        {
            p.AiMade = ai.IsChecked == true;
            Packages.Save(p);
        };
        more.Children.Add(ai);
        MirrorRow(left, p);

        // Use, export, delete.
        left.Children.Add(new Border { Style = S("Divider"), Margin = new Thickness(0, 14, 0, 0) });
        var use = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        void Add(Panel panel, UIElement e)
        {
            if (e is FrameworkElement f) f.Margin = new Thickness(0, 0, 8, 8);
            panel.Children.Add(e);
        }
        if (p.Images.Count > 0 && c.Wallpaper.PackageId != p.Id)
            Add(use, Locked(Ui.Button(Strings.T("Use for the desktop", "Für den Desktop verwenden"), (_, _) => ChangeAndRebuild(x =>
            {
                x.Wallpaper.PackageId = p.Id;
                x.Wallpaper.Enabled = true;
            }, BuildBackground)), Policies.ManagedPackageFolder));
        if (p.Images.Count > 0 && !(c.LockScreen.Enabled && c.LockScreen.Source == LockScreenSource.Package && c.LockScreen.PackageId == p.Id))
            Add(use, Locked(Ui.Button(Strings.T("Use for the lock screen", "Für den Sperrbildschirm verwenden"), (_, _) => SetLock(LockScreenSource.Package, p.Id)), Policies.LockScreenEnabled));
        Add(use, Ui.Button(Strings.T("Export as file", "Als Datei exportieren"), (_, _) => ExportPackage(p))
            .Tip(Strings.T("One .zip with the pictures, their times and the descriptions, to keep or to add on another PC.",
                "Eine .zip mit den Bildern, ihren Zeiten und den Beschreibungen, zum Aufbewahren oder für einen anderen PC.")));
        var delete = Ui.Button(Strings.T("Delete", "Löschen"), (_, _) => _ = DeletePackageAsync(p))
            .Tip(Strings.T("Removes the package and its copied pictures from DynaTones. Asks first.", "Entfernt das Paket und die kopierten Bilder aus DynaTones. Fragt vorher nach."));
        Add(use, delete);
        left.Children.Add(use);

        // ---------- Right: the pictures ----------
        var right = new StackPanel();
        var rightCard = new Border { Style = S("Card"), Child = right, Padding = new Thickness(20, 16, 20, 16), VerticalAlignment = VerticalAlignment.Top };
        Grid.SetColumn(rightCard, 2);
        columns.Children.Add(rightCard);
        var title = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        title.Children.Add(new TextBlock { Style = S("CardTitle"), Text = Strings.T("Pictures", "Bilder") });
        title.Children.Add(new TextBlock { Style = S("Muted"), Text = Strings.T("Dragging changes the order", "Ziehen ändert die Reihenfolge"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
        right.Children.Add(title);
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(-4, 0, -4, 0) };
        var day = new SunDay(DateOnly.FromDateTime(DateTime.Now), (Engine.Position ?? Locator.FromTimeZone())?.Latitude ?? 50, (Engine.Position ?? Locator.FromTimeZone())?.Longitude ?? 10);
        var slots = p.Images.Count > 0 ? Planner.Day(p, day) : [];
        for (var i = 0; i < p.Images.Count; i++) grid.Children.Add(PackageImageTile(p, i, slots));
        grid.Children.Add(AddImagesTile(p));
        right.Children.Add(grid);
        right.Children.Add(new TextBlock { Style = S("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            Text = Strings.T("A click on a picture sets when it begins. Remove one with its context menu or Del.",
                "Ein Klick auf ein Bild stellt ein, wann es beginnt. Entfernen über das Kontextmenü oder Entf.") });
    }

    /// <summary>One picture of the package: click opens it, drag moves it, Del or the menu removes it.</summary>
    UIElement PackageImageTile(Package p, int index, IReadOnlyList<Slot> slots)
    {
        var image = p.Images[index];
        var stack = new StackPanel();
        var picture = Picture(p.PathOf(image), double.NaN, 64, 240);
        picture.CornerRadius = new CornerRadius(6);
        stack.Children.Add(picture);
        stack.Children.Add(new TextBlock { Style = S("RowLabel"), Text = PictureName(image), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
        var starts = slots.Where(s => s.Image == index).Select(s => Strings.Time(s.Start)).ToList();
        var how = image.Anchor.Mode switch
        {
            AnchorMode.Auto => Strings.T(", Auto", ", Auto"),
            AnchorMode.Time => Strings.T(" o'clock", " Uhr"),
            _ => "",
        };
        stack.Children.Add(new TextBlock
        {
            Style = S("Muted"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = image.Anchor.Mode == AnchorMode.Off ? Strings.T("not shown", "wird nicht gezeigt")
                : starts.Count == 0 ? Strings.T("not today", "heute nicht")
                : Strings.T("from ", "ab ") + string.Join(", ", starts) + how,
        });
        var button = new Button
        {
            Style = S("IconButton"),
            Width = double.NaN,
            Height = double.NaN,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            FontFamily = new FontFamily("Segoe UI"),
            Content = stack,
            AllowDrop = true,
        };
        Speak(button, PictureName(image));
        button.ToolTip = Strings.T("Click: when it begins. Drag: another place in the order. Del: remove.", "Klick: wann es beginnt. Ziehen: andere Stelle in der Reihenfolge. Entf: entfernen.");
        button.Click += (_, _) => OpenPage(BgPage.Image, p.Id, index);
        button.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Delete) return;
            e.Handled = true;
            RemoveImage(p, image);
        };
        var menu = new ContextMenu();
        // An icon, the verb first, the key at the right; the dangerous entry red (design system, "Menus").
        var remove = new MenuItem { Header = Strings.T("Remove", "Entfernen"), InputGestureText = Strings.T("Del", "Entf"), Tag = "", Style = S("DangerMenuItem") };
        remove.Click += (_, _) => RemoveImage(p, image);
        menu.Items.Add(remove);
        button.ContextMenu = menu;

        // Dragging: a few pixels of movement with the button pressed starts it; the drop place takes the picture.
        Point? down = null;
        button.PreviewMouseLeftButtonDown += (_, e) => down = e.GetPosition(button);
        button.PreviewMouseMove += (_, e) =>
        {
            if (down is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            var now = e.GetPosition(button);
            if (Math.Abs(now.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            down = null;
            DragDrop.DoDragDrop(button, new DataObject(ImageDragFormat, index), DragDropEffects.Move);
        };
        button.PreviewMouseLeftButtonUp += (_, _) => down = null;
        button.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(ImageDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        button.Drop += (_, e) =>
        {
            if (e.Data.GetData(ImageDragFormat) is not int from) return;
            e.Handled = true;
            Packages.Move(p, from, index);
            SavePackage(p);
        };
        return button;
    }

    UIElement AddImagesTile(Package p)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock { FontFamily = (FontFamily)FindResource("IconFont"), Text = "", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Style = S("RowLabel"), Text = Strings.T("Add pictures", "Bilder hinzufügen"), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button { Style = S("SecondaryButton"), Height = double.NaN, Margin = new Thickness(4, 4, 4, 8), VerticalAlignment = VerticalAlignment.Stretch, Content = stack };
        button.ToolTip = Strings.T("Adds JPEG or PNG pictures; Auto places them in the day.", "Fügt JPEG- oder PNG-Bilder hinzu; Auto verteilt sie über den Tag.");
        Speak(button, Strings.T("Add pictures", "Bilder hinzufügen"));
        button.Click += async (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = Strings.T("Pictures", "Bilder") + " (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            };
            if (dialog.ShowDialog(this) != true) return;
            button.IsEnabled = false;
            try
            {
                await Task.Run(() => Packages.AddImages(p, dialog.FileNames));
            }
            catch (Exception ex)
            {
                Log.Error("Adding pictures failed", ex);
                _ = this.dialog.ShowMessageAsync(Strings.T("The pictures could not be added", "Die Bilder konnten nicht hinzugefügt werden"), ex.Message, problem: true);
            }
            SavePackage(p);
        };
        return button;
    }

    /// <summary>Takes a picture out, with Undo for a few seconds; its file goes only when the time is up.</summary>
    void RemoveImage(Package p, PackageImage image)
    {
        var index = p.Images.IndexOf(image);
        if (index < 0) return;
        Packages.Remove(p, image);
        SavePackage(p);
        dialog.ShowUndo(Strings.T($"\"{PictureName(image)}\" removed.", $"„{PictureName(image)}“ entfernt."),
            undo: () =>
            {
                Packages.Restore(p, image, index);
                SavePackage(p);
            },
            expired: () => Packages.DeleteLeftovers(p));
    }

    void ExportPackage(Package p)
    {
        var save = new Microsoft.Win32.SaveFileDialog
        {
            FileName = p.DisplayName + ".zip",
            Filter = Strings.T("Image package", "Bildpaket") + " (*.zip)|*.zip",
        };
        if (save.ShowDialog(this) != true) return;
        try
        {
            Packages.Export(p, save.FileName);
        }
        catch (Exception ex)
        {
            Log.Error("Exporting the package failed", ex);
            _ = dialog.ShowMessageAsync(Strings.T("The package could not be exported", "Das Paket konnte nicht exportiert werden"), ex.Message, problem: true);
        }
    }

    /// <summary>"New" in the add menu: an empty package, opened for editing.</summary>
    void NewPackage()
    {
        var package = Packages.CreateEmpty(Strings.T("New package", "Neues Paket"));
        OpenPackage(package.Id);
    }
}
