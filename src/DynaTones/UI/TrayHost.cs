using System.Windows;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>The app while it runs: the engine, the tray icon, the message pipe and, when open, the window.</summary>
public sealed class TrayHost : IDisposable
{
    KitTray? tray;
    readonly Ipc.Server? server;
    TrayEntry? updateItem, nextItem, switchItem, jumpItem, pauseItem;
    public DayEngine Engine { get; }
    MainWindow? window;

    public TrayHost(bool showWindow, bool dryRun = false)
    {
        if (dryRun) DayEngine.DryRun = true;
        Engine = new DayEngine(Application.Current.Dispatcher);
        Engine.Updated += UpdateTray;
        // Switched on at the end of the introduction (or with --switch-on): from now on the day is on screen.
        KitIntro.SwitchedOn += () => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Engine.Reload();
            Engine.Resolve("switched on");
        });
        // The window's own color mode from the settings ("Like Windows" unless chosen otherwise); until 0.3.0 every
        // start went back to "Like Windows" whatever was chosen.
        var look = ConfigStore.Load();
        Theme.Apply(look.WindowTheme, look.Accent);
        // The plan on screen before anything else: the first resolve runs here, before the tray icon, the pipe and
        // the window (STANDARD, start order).
        var start = Engine.StartAsync();

        if (!dryRun)
        {
            tray = new KitTray(ShowWindow);
            KitUpdate.Notify = tray.Notify;
            FillTray(tray);
            UpdateTray();
            server = new Ipc.Server(message => Application.Current.Dispatcher.BeginInvoke(() => Handle(message)));
            // After an import or restore under About, "Backup and restore".
            KitSettings.Reload = () =>
            {
                Engine.Reload();
                Engine.Resolve("settings imported");
            };
        }
        _ = start;
        if (showWindow) ShowWindow();
    }

    /// <summary>The app's entries in the family's tray menu.</summary>
    void FillTray(KitTray tray)
    {
        // The family's tray menu (kit KitTray): the head opens the window and says what holds now; then what the
        // day does, Pause with its state at the right, and Quit (the kit's).
        updateItem = tray.Add(Strings.T("Update now", "Jetzt aktualisieren"), () =>
        {
            Engine.Reload();
            Engine.Resolve("request");
        }, "");
        nextItem = tray.Add(Strings.T("Next image", "Nächstes Bild"), () => Engine.SkipNext(), "");
        // The color mode: the other one now, or the whole next phase with its image; the words follow the mode.
        switchItem = tray.Add(Strings.T("Switch to dark", "Auf Dunkel umschalten"), () => Engine.SwitchMode(), "");
        jumpItem = tray.Add(Strings.T("Jump to the next dark phase", "Zur nächsten dunklen Phase springen"), () => Engine.JumpToMode(), "");
        tray.AddSeparator();
        pauseItem = tray.AddMenu(Strings.T("Pause", "Pausieren"), "",
            (Strings.T("For an hour", "Für eine Stunde"), () => Pause(DateTime.Now.AddHours(1))),
            (Strings.T("Until tomorrow morning", "Bis morgen früh"), () => Pause(TomorrowMorning)),
            (Strings.T("Until I resume", "Bis ich fortsetze"), () => Pause(Forever)),
            (Strings.T("Resume", "Fortsetzen"), () => Pause(null)));
    }

    /// <summary>
    /// For --snapshot: the tray menu as pictures at the screen's size and at 150 percent, with the menu as it is now;
    /// a tray icon shows for the moment it takes.
    /// </summary>
    internal void SaveTrayPictures(string folder, string suffix)
    {
        var own = tray == null;
        tray ??= new KitTray(ShowWindow);
        if (own) FillTray(tray);
        UpdateTray();
        tray.SaveMenuPicture(Path.Combine(folder, $"tray-100-{suffix}.png"), 1);
        tray.SaveMenuPicture(Path.Combine(folder, $"tray-150-{suffix}.png"), 1.5);
        if (!own) return;
        tray.Dispose();
        tray = null;
        updateItem = nextItem = switchItem = jumpItem = pauseItem = null;
    }

    void Handle(string message)
    {
        var command = message.Split('\n')[0].Trim().ToLowerInvariant();
        switch (command)
        {
            case "show":
                ShowWindow();
                break;
            case "refresh":
                Engine.Reload();
                Engine.Resolve("request");
                break;
            // From a sibling (StartQueue) or a second start with --next: the next image of the day now.
            case "next":
                Engine.SkipNext();
                break;
            // --switch-mode: the other color mode until the plan comes there; --jump-mode: the next phase of the other
            // mode with its picture (again: back to the plan).
            case "switch-mode":
                Engine.SwitchMode();
                break;
            case "jump-mode":
                Engine.JumpToMode();
                break;
            // --switch-on: as "Switch DynaTones on" at the end of the introduction (deployment, tests).
            case "switch-on":
                KitIntro.SwitchOn();
                break;
            // Sent by --import from the command line, after the settings were replaced.
            case KitSettings.ReloadMessage:
                Engine.Reload();
                Engine.Resolve("settings imported");
                break;
            case KitSetup.QuitCommand:
                Application.Current.Shutdown();
                break;
        }
    }

    public void ShowWindow()
    {
        if (window == null)
        {
            window = new MainWindow(this);
            window.Closed += (_, _) => window = null;
        }
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>Paused until resumed by hand: a date no day reaches.</summary>
    public static readonly DateTime Forever = new(9999, 12, 31);

    /// <summary>Tomorrow at 06:00, for "until tomorrow morning".</summary>
    public static DateTime TomorrowMorning => DateTime.Today.AddDays(1).AddHours(6);

    public static bool IsForever(DateTime? until) => until is { } u && u.Year >= 9999;

    public void Pause(DateTime? until)
    {
        ConfigStore.Update(c => c.PausedUntil = until);
        Engine.Reload();
        Engine.Resolve(until == null ? "resume" : "pause");
    }

    void UpdateTray()
    {
        if (tray == null) return;
        // Not switched on yet: nothing changes, so the tray says so and offers no color mode.
        var off = KitIntro.Pending;
        var plan = Engine.Plan;
        var paused = Engine.Settings.PausedUntil is { } until && until > DateTime.Now;
        tray.Status = TrayStatus(off, paused, plan, Engine.Settings);
        if (updateItem != null) updateItem.Visible = !off;
        if (nextItem != null) nextItem.Enabled = !off && plan?.Next != null && Engine.Settings.Wallpaper.Enabled;
        if (nextItem != null) nextItem.Visible = !off;
        if (pauseItem != null)
        {
            pauseItem.Visible = !off;
            pauseItem.Value = !paused ? Strings.T("Running", "Läuft")
                : IsForever(Engine.Settings.PausedUntil) ? Strings.T("until you resume", "bis du fortsetzt")
                : Strings.T($"until {Strings.Time(Engine.Settings.PausedUntil!.Value)}", $"bis {Strings.Time(Engine.Settings.PausedUntil!.Value)}");
        }
        if (switchItem != null)
        {
            switchItem.Text = SwitchText(plan);
            switchItem.Icon = (plan?.Dark ?? Appearance.AppsLight == false) ? "" : "";
            switchItem.Visible = !off;
        }
        if (jumpItem != null)
        {
            jumpItem.Text = JumpText(plan);
            jumpItem.Visible = !off && plan?.Dark != null && (plan.ModeHeld || plan.NextModeChange != null);
        }
    }

    /// <summary>
    /// The menu's head and the icon's tooltip in one short sentence, as the window's header says it: "Light mode, next
    /// change at 14:50", "Paused until 15:00". Windows cuts the tooltip at 63 characters, so no more than that.
    /// </summary>
    public static string TrayStatus(bool off, bool paused, Plan? plan, Config settings)
    {
        if (off) return Strings.T("Not switched on yet", "Noch nicht eingeschaltet");
        if (paused)
            return IsForever(settings.PausedUntil)
                ? Strings.T("Paused until you resume", "Pausiert, bis du fortsetzt")
                : Strings.T($"Paused until {Strings.Time(settings.PausedUntil!.Value)}", $"Pausiert bis {Strings.Time(settings.PausedUntil!.Value)}");
        var parts = new List<string>();
        if (plan?.Dark is { } dark) parts.Add(dark ? Strings.T("Dark mode", "Dunkler Modus") : Strings.T("Light mode", "Heller Modus"));
        if (plan?.NextAnnounced is { } next)
        {
            var at = next.Date == DateTime.Today ? Strings.Time(next) : $"{next.ToString("ddd", Strings.Culture)} {Strings.Time(next)}";
            parts.Add(parts.Count == 0 ? Strings.T($"Next change at {at}", $"Nächster Wechsel um {at}") : Strings.T($"next change at {at}", $"nächster Wechsel um {at}"));
        }
        return string.Join(", ", parts);
    }

    /// <summary>"Switch to dark" or "light": the mode not shown now; without DynaTones' color mode, Windows' own.</summary>
    public static string SwitchText(Plan? plan) =>
        (plan?.Dark ?? Appearance.AppsLight == false)
            ? Strings.T("Switch to light", "Auf Hell umschalten")
            : Strings.T("Switch to dark", "Auf Dunkel umschalten");

    /// <summary>The next phase of the other mode with its image, or back to the plan while a mode is held.</summary>
    public static string JumpText(Plan? plan) =>
        plan?.ModeHeld == true ? Strings.T("Back to the plan", "Zurück zum Plan")
        : plan?.Dark == true ? Strings.T("Jump to the next light phase", "Zur nächsten hellen Phase springen")
        : Strings.T("Jump to the next dark phase", "Zur nächsten dunklen Phase springen");

    public void Dispose()
    {
        Engine.Dispose();
        server?.Dispose();
        tray?.Dispose();
    }
}
