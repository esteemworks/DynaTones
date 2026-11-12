using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynaTones.Core;

namespace DynaTones.UI;

/// <summary>
/// Draws each page of the window into a PNG, light and dark, for checking the layout and for the README. The window
/// opens far off screen and never takes the focus; nothing is applied.
/// </summary>
static class Snapshot
{
    /// <summary>
    /// How much sharper than the screen: <c>--snapshot &lt;folder&gt; 4</c> draws at four times the size, for the README
    /// and the film, which then show the pictures at most at their recorded size. Without it the screen's own scale.
    /// </summary>
    static double scale;

    /// <summary>The pictures are read off the window's thread: a page is drawn into the file once they are all there.</summary>
    static async Task PicturesRead()
    {
        for (var i = 0; i < 100 && ThumbCache.Busy; i++) await Task.Delay(100);
        await Task.Delay(100);
    }

    public static void Run(string folder, Application app)
    {
        var args = Environment.GetCommandLineArgs();
        scale = args.Length > 3 && double.TryParse(args[3], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var s) && s is > 0 and <= 8 ? s : 0;
        Directory.CreateDirectory(folder);
        // `--snapshot <folder> <scale> intro`: the introduction of a new installation and looking around, in this
        // process only (nothing is written to the data folder).
        var intro = args.Length > 4 && args[4] == "intro";
        if (intro) KitIntro.ForSnapshot = true;
        var host = new TrayHost(showWindow: false, dryRun: true);
        var window = new MainWindow(host)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -30000,
            Top = -30000,
        };
        window.Show();

        app.Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(1500);
            if (intro)
            {
                foreach (var (theme, mode) in new[] { (ThemeChoice.Light, "light"), (ThemeChoice.Dark, "dark") })
                {
                    Theme.Apply(theme, AccentChoice.Brand);
                    foreach (var (index, name) in new[] { (0, "welcome"), (1, "place"), (2, "ready"), (-1, "looking") })
                    {
                        window.ShowIntroForSnapshot(index);
                        await Task.Delay(500);
                        await PicturesRead();
                        window.UpdateLayout();
                        Save(window, Path.Combine(folder, $"intro-{name}-{mode}.png"));
                        var introScroller = Scrollers(window).FirstOrDefault(v => v.IsVisible && v.ScrollableHeight > 0);
                        if (introScroller != null)
                        {
                            introScroller.ScrollToEnd();
                            await Task.Delay(300);
                            await PicturesRead();
                            window.UpdateLayout();
                            Save(window, Path.Combine(folder, $"intro-{name}-end-{mode}.png"));
                            introScroller.ScrollToHome();
                        }
                    }
                }
                host.Dispose();
                app.Shutdown();
                return;
            }
            // `--snapshot <folder> <scale> accents`: every page with each accent of the settings, named <page>-<accent>-<mode>.
            var accents = args.Length > 4 && args[4] == "accents" ? [.. EsteemWorks.Kit.UI.AccentRow.Choices(profiles: true)] : new[] { AccentChoice.Brand };
            foreach (var accent in accents)
            foreach (var (theme, mode) in new[] { (ThemeChoice.Light, "light"), (ThemeChoice.Dark, "dark") })
            {
                var suffix = accents.Length > 1 ? $"{accent.ToString().ToLowerInvariant()}-{mode}" : mode;
                Theme.Apply(theme, accent);
                window.ShowBackgroundPage("overview");
                foreach (var tab in window.Tabs)
                {
                    tab.Button.IsChecked = true;
                    await Task.Delay(400);
                    await PicturesRead();
                    window.UpdateLayout();
                    Save(window, Path.Combine(folder, $"{tab.Name}-{suffix}.png"));
                    // A page longer than the window: every further screen, the last one as -end, so nothing below the
                    // fold goes unseen; 60 px overlap, so nothing is cut in half between two pictures (kit template).
                    var scroller = Scrollers(window).FirstOrDefault(v => v.IsVisible && v.ScrollableHeight > 0);
                    if (scroller != null)
                    {
                        var step = Math.Max(100, scroller.ViewportHeight - 60);
                        for (var n = 2; scroller.VerticalOffset < scroller.ScrollableHeight; n++)
                        {
                            scroller.ScrollToVerticalOffset(Math.Min(scroller.ScrollableHeight, scroller.VerticalOffset + step));
                            await Task.Delay(300);
                            await PicturesRead();
                            window.UpdateLayout();
                            var last = scroller.VerticalOffset >= scroller.ScrollableHeight - 0.5;
                            Save(window, Path.Combine(folder, last ? $"{tab.Name}-end-{suffix}.png" : $"{tab.Name}-{n}-{suffix}.png"));
                        }
                        scroller.ScrollToHome();
                    }
                }
                // The pages under Background, one after the other.
                foreach (var name in MainWindow.BackgroundPages)
                {
                    window.ShowBackgroundPage(name);
                    await Task.Delay(500);
                    await PicturesRead();
                    window.UpdateLayout();
                    Save(window, Path.Combine(folder, $"background-{name}-{suffix}.png"));
                    var pageScroller = Scrollers(window).FirstOrDefault(v => v.IsVisible && v.ScrollableHeight > 0);
                    if (pageScroller != null)
                    {
                        pageScroller.ScrollToEnd();
                        await Task.Delay(300);
                        await PicturesRead();
                        window.UpdateLayout();
                        Save(window, Path.Combine(folder, $"background-{name}-end-{suffix}.png"));
                        pageScroller.ScrollToHome();
                    }
                }
                window.ShowBackgroundPage("overview");
                // The gallery, reached from Background.
                await window.ShowGalleryForSnapshot();
                await Task.Delay(600);
                await PicturesRead();
                window.UpdateLayout();
                Save(window, Path.Combine(folder, $"gallery-{suffix}.png"));
                var galleryScroller = Scrollers(window).FirstOrDefault(v => v.IsVisible && v.ScrollableHeight > 0);
                if (galleryScroller != null)
                {
                    galleryScroller.ScrollToEnd();
                    await Task.Delay(300);
                    await PicturesRead();
                    window.UpdateLayout();
                    Save(window, Path.Combine(folder, $"gallery-end-{suffix}.png"));
                    galleryScroller.ScrollToHome();
                }
                // The profile editor, as "New profile".
                window.ShowProfileEditorForSnapshot(true);
                await Task.Delay(500);
                window.UpdateLayout();
                Save(window, Path.Combine(folder, $"profile-edit-{suffix}.png"));
                window.ShowProfileEditorForSnapshot(false);
                // The in-window question and the Undo bar.
                window.ShowSampleDialog(true);
                await Task.Delay(400);
                window.UpdateLayout();
                Save(window, Path.Combine(folder, $"dialog-{suffix}.png"));
                window.ShowSampleDialog(false);
                // The tray menu, drawn without opening it, at the screen's size and at 150 percent.
                host.SaveTrayPictures(folder, suffix);
            }
            host.Dispose();
            app.Shutdown();
        });
    }

    static IEnumerable<System.Windows.Controls.ScrollViewer> Scrollers(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.ScrollViewer scroller) yield return scroller;
            foreach (var inner in Scrollers(child)) yield return inner;
        }
    }

    static void Save(Window window, string path)
    {
        var root = (FrameworkElement)window.Content;
        var screen = VisualTreeHelper.GetDpi(window);
        // WPF draws vectors, so a higher resolution is sharper, not blown up.
        var factorX = scale > 0 ? scale : screen.DpiScaleX;
        var factorY = scale > 0 ? scale : screen.DpiScaleY;
        var bitmap = new RenderTargetBitmap((int)(root.ActualWidth * factorX), (int)(root.ActualHeight * factorY),
            96 * factorX, 96 * factorY, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        bitmap.Render(background);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path))
            encoder.Save(file);
        WriteBounds(root, Path.ChangeExtension(path, ".json"));
    }

    /// <summary>
    /// Beside each picture, where its controls and texts are, in device independent pixels of the window (the picture
    /// has these times the snapshot's factor): for marks in the film and the README that sit exactly on an element
    /// instead of being placed by eye.
    /// </summary>
    static void WriteBounds(FrameworkElement root, string path)
    {
        var items = new List<object>();
        void Walk(DependencyObject node)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                if (VisualTreeHelper.GetChild(node, i) is not FrameworkElement child || !child.IsVisible) continue;
                var text = child switch
                {
                    System.Windows.Controls.TextBlock t => t.Text,
                    System.Windows.Controls.TextBox t => t.Text,
                    System.Windows.Controls.ComboBox c => c.Text,
                    _ => System.Windows.Automation.AutomationProperties.GetName(child),
                };
                var kind = child.GetType().Name;
                var wanted = child is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.ComboBox or System.Windows.Controls.TextBox
                    or System.Windows.Controls.TextBlock or System.Windows.Controls.Image
                    || (child is System.Windows.Controls.Border b && b.BorderThickness.Left > 0);
                if (wanted && child.ActualWidth > 0 && child.ActualHeight > 0)
                {
                    try
                    {
                        var r = child.TransformToAncestor(root).TransformBounds(new Rect(0, 0, child.ActualWidth, child.ActualHeight));
                        if (r.Right > 0 && r.Bottom > 0 && r.Left < root.ActualWidth && r.Top < root.ActualHeight)
                            items.Add(new { kind, text = string.IsNullOrEmpty(text) ? null : text, x = Math.Round(r.X, 1), y = Math.Round(r.Y, 1), w = Math.Round(r.Width, 1), h = Math.Round(r.Height, 1) });
                    }
                    catch (InvalidOperationException)
                    {
                        // Not in this window's tree (a popup): left out.
                    }
                }
                Walk(child);
            }
        }
        Walk(root);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { width = root.ActualWidth, height = root.ActualHeight, items },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
