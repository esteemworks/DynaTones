using System.Windows;
using DynaTones.Core;
using EsteemWorks.Kit.UI;

namespace DynaTones.UI;

/// <summary>The app while it runs: the engine, the tray icon, the message pipe and, when open, the window.</summary>
public sealed class TrayHost : IDisposable
{
    readonly KitTray? tray;
    readonly Ipc.Server? server;
    readonly System.Windows.Forms.ToolStripMenuItem? switchItem, jumpItem;
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
            tray.Add(Strings.T("Update now", "Jetzt aktualisieren"), () =>
            {
                Engine.Reload();
                Engine.Resolve("request");
            });
            tray.Add(Strings.T("Next image", "Nächstes Bild"), () => Engine.SkipNext());
            // The color mode: the other one now, or the whole next phase with its image; the words follow the mode.
            switchItem = tray.Add(Strings.T("Switch to dark", "Auf Dunkel umschalten"), () => Engine.SwitchMode());
            jumpItem = tray.Add(Strings.T("Next dark phase, with its picture", "Nächste dunkle Phase, mit Bild"), () => Engine.JumpToMode());
            tray.AddMenu(Strings.T("Pause", "Pausieren"),
                (Strings.T("For an hour", "Für eine Stunde"), () => Pause(DateTime.Now.AddHours(1))),
                (Strings.T("Until tomorrow morning", "Bis morgen früh"), () => Pause(TomorrowMorning)),
                (Strings.T("Until I resume", "Bis ich fortsetze"), () => Pause(Forever)),
                (Strings.T("Resume", "Fortsetzen"), () => Pause(null)));
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
        tray.Status = off ? Strings.T("Not switched on yet", "Noch nicht eingeschaltet")
            : Engine.Plan?.NextAnnounced is { } next ? Strings.T("Next change at ", "Nächster Wechsel um ") + Strings.Time(next) : "";
        var plan = Engine.Plan;
        if (switchItem != null)
        {
            switchItem.Text = SwitchText(plan);
            switchItem.Visible = !off;
        }
        if (jumpItem != null)
        {
            jumpItem.Text = JumpText(plan);
            jumpItem.Visible = !off && plan?.Dark != null && (plan.ModeHeld || plan.NextModeChange != null);
        }
    }

    /// <summary>"Switch to dark" or "light": the mode not shown now; without DynaTones' color mode, Windows' own.</summary>
    public static string SwitchText(Plan? plan) =>
        (plan?.Dark ?? Appearance.AppsLight == false)
            ? Strings.T("Switch to light", "Auf Hell umschalten")
            : Strings.T("Switch to dark", "Auf Dunkel umschalten");

    /// <summary>The next phase of the other mode with its image, or back to the plan while a mode is held.</summary>
    public static string JumpText(Plan? plan) =>
        plan?.ModeHeld == true ? Strings.T("Back to the plan", "Zurück zum Plan")
        : plan?.Dark == true ? Strings.T("Next light phase, with its picture", "Nächste helle Phase, mit Bild")
        : Strings.T("Next dark phase, with its picture", "Nächste dunkle Phase, mit Bild");

    public void Dispose()
    {
        Engine.Dispose();
        server?.Dispose();
        tray?.Dispose();
    }
}
