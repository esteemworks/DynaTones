using System.Windows.Threading;
using Microsoft.Win32;

namespace DynaTones.Core;

/// <summary>
/// Keeps the screen on the plan. One function, <see cref="Resolve"/>, compares what should be shown with what is and
/// changes only what differs, so calling it again changes nothing. It runs at the exact moment of the next change,
/// after every event that can disturb the screen (waking up, unlocking, a changed clock, a new monitor, someone else
/// setting a background) and once a minute as a safety net.
/// </summary>
public sealed class DayEngine : IDisposable
{
    readonly Dispatcher dispatcher;
    readonly DispatcherTimer exact;
    readonly DispatcherTimer minute;
    DateTime lastLocationAsk = DateTime.MinValue;
    bool disposed;

    /// <summary>
    /// With DYNATONES_DRYRUN=1 everything is calculated and shown, but nothing on screen is changed: for trying
    /// DynaTones next to another wallpaper or theme tool, and for tests.
    /// </summary>
    public static bool DryRun { get; set; } = Environment.GetEnvironmentVariable("DYNATONES_DRYRUN") == "1";

    /// <summary>The settings as they apply now: <see cref="Settings"/> with the active profile's values.</summary>
    public Config Config { get; private set; } = new();
    /// <summary>The settings as saved, without a profile: what the pages under Background and Color mode set.</summary>
    public Config Settings { get; private set; } = new();
    /// <summary>The profile that applies now; null: none, the settings as they are.</summary>
    public FamilyProfile? ActiveProfile { get; private set; }
    public Package? Package { get; private set; }
    /// <summary>The lock screen's own package, when it does not follow the wallpaper.</summary>
    public Package? LockPackage { get; private set; }
    /// <summary>Why the lock screen cannot follow the day right now; null when all is well or it is off.</summary>
    public string? LockProblem { get; private set; }
    bool lockBusy;
    bool blendBusy;
    (string, string)? blendFailed;
    public Position? Position { get; private set; }
    public Plan? Plan { get; private set; }
    /// <summary>Why something cannot be done right now, for the window; null when all is well.</summary>
    public string? Problem { get; private set; }
    public DateTime? LastWallpaperChange { get; private set; }
    public string? LastReason { get; private set; }

    /// <summary>After every resolve, on the UI thread.</summary>
    public event Action? Updated;

    public DayEngine(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        exact = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
        exact.Tick += (_, _) => Resolve("scheduled");
        minute = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMinutes(1) };
        minute.Tick += (_, _) => Resolve("check");
    }

    public async Task StartAsync()
    {
        Reload();
        SystemEvents.PowerModeChanged += OnPower;
        SystemEvents.SessionSwitch += OnSession;
        SystemEvents.TimeChanged += OnTime;
        SystemEvents.DisplaySettingsChanged += OnDisplay;
        SystemEvents.UserPreferenceChanged += OnPreference;
        minute.Start();
        // Profiles changed here or in another app of the family: what applies is read again.
        FamilyProfiles.Changed += () =>
        {
            Reload();
            Resolve("profiles");
        };
        Resolve("start");
        await RefreshLocationAsync(force: false);
    }

    /// <summary>Settings or the package changed: read them again and resolve.</summary>
    public void Reload()
    {
        Settings = ConfigStore.Load();
        ActiveProfile = Profiles.Active(DateTime.Now);
        Config = ProfileOverrides.Apply(Settings, ActiveProfile?.Id);
        Strings.Choice = Config.Language;
        Package = Packages.Load(Config.Wallpaper.PackageId);
        LockPackage = Config.LockScreen.Source == LockScreenSource.Package ? Packages.Load(Config.LockScreen.PackageId) : null;
        Position = Locator.ResolveCached(Config.Location);
    }

    /// <summary>Asks Windows for the position again (at most once a day unless forced) and resolves with it.</summary>
    public async Task RefreshLocationAsync(bool force)
    {
        if (!force && DateTime.Now - lastLocationAsk < TimeSpan.FromHours(6)) return;
        lastLocationAsk = DateTime.Now;
        var settings = Config.Location;
        if (force) settings.LastWindows = null;
        var position = await Locator.ResolveAsync(settings, askWindows: true);
        if (settings.LastWindows != null)
            ConfigStore.Update(c => c.Location.LastWindows = settings.LastWindows);
        Position = position;
        Resolve(force ? "location" : "location check");
    }

    /// <summary>
    /// "Next image": shows the next image of the day now and keeps the plan one step ahead until that image's own
    /// time, so neither the next check nor the flowing transition brings the skipped one back. Again: one further.
    /// False when there is no next image.
    /// </summary>
    public bool SkipNext()
    {
        if (Plan?.Next is not { } next) return false;
        ConfigStore.Update(c => c.Wallpaper.SkipTo = next.Start);
        Log.Info($"Next image: {Package?.Images[next.Image].File ?? next.Image.ToString()} ahead of {next.Start:HH:mm}");
        Reload();
        Resolve("next image", byHand: true);
        return true;
    }

    /// <summary>Back to the plan after "Next image" or a color mode set by hand.</summary>
    public void BackToPlan()
    {
        if (Config.Wallpaper.SkipTo == null && Config.Appearance.Hold == null) return;
        ConfigStore.Update(c =>
        {
            c.Wallpaper.SkipTo = null;
            c.Appearance.Hold = null;
        });
        Log.Info("Back to the plan");
        Reload();
        Resolve("back to plan", byHand: true);
    }

    /// <summary>
    /// "Switch to dark" or "light": the other color mode now, the background as it is, until the plan comes to that
    /// mode by itself; from then on the day goes on as planned. Again while it is held: back to the plan's mode. When
    /// DynaTones does not switch the color mode, it switches Windows' own, as Settings would.
    /// </summary>
    public void SwitchMode()
    {
        if (Plan?.ModeHeld == true)
        {
            ConfigStore.Update(c => c.Appearance.Hold = null);
            Log.Info("Color mode back to the plan");
        }
        else if (Plan is { } plan && Schedule.SwitchMode(plan) is { } hold)
        {
            ConfigStore.Update(c => c.Appearance.Hold = hold);
            Log.Info($"Color mode {(hold.Dark ? "dark" : "light")} by hand until {hold.Until:dd.MM. HH:mm}");
        }
        else
        {
            // Not DynaTones' to plan: Windows' own mode, apps and Windows together, as they are now for apps.
            var light = !(Appearance.AppsLight ?? true);
            if (!DryRun && !KitIntro.Pending) Appearance.Apply(light, light);
            Log.Info($"Windows color mode {(light ? "light" : "dark")} by hand");
            Updated?.Invoke();
            return;
        }
        Reload();
        Resolve("color mode by hand", byHand: true);
    }

    /// <summary>
    /// "Next dark (or light) phase": color mode and background as they will be when the plan next comes to the other
    /// mode, now; both held until then, from then on the day goes on as planned. While a mode is held: back to the plan.
    /// False when there is no such moment.
    /// </summary>
    public bool JumpToMode()
    {
        if (Plan?.ModeHeld == true)
        {
            BackToPlan();
            return true;
        }
        if (Plan is not { } plan || Schedule.Jump(Config, Package, Position, plan) is not { } jump) return false;
        var (hold, skipTo) = jump;
        ConfigStore.Update(c =>
        {
            c.Appearance.Hold = hold;
            c.Wallpaper.SkipTo = skipTo;
        });
        Log.Info($"Jump to {(hold.Dark ? "dark" : "light")} as at {hold.Until:dd.MM. HH:mm}" +
                 (skipTo is { } at ? $", the image from {at:HH:mm} ahead of its time" : ", the image stays"));
        Reload();
        Resolve("jump to mode", byHand: true);
        return true;
    }

    /// <param name="byHand">
    /// Next image, a color mode or a jump chosen by the user: shown also while paused, and the pause then keeps it.
    /// </param>
    public void Resolve(string reason, bool byHand = false)
    {
        if (disposed) return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Resolve(reason, byHand));
            return;
        }

        var now = DateTime.Now;
        // Another profile from now on (its hours began or ended, or it was switched by hand): its settings first.
        if (Profiles.Active(now)?.Id != ActiveProfile?.Id)
        {
            Reload();
            Log.Info(ActiveProfile == null ? "No profile applies now" : $"Profile \"{ActiveProfile.Name}\" applies now");
        }
        // A skip ends by itself when its image's own time has come; then it is the plan anyway.
        if (Config.Wallpaper.SkipTo is { } skipTo && skipTo.AddMinutes(20) <= now)
        {
            ConfigStore.Update(c => c.Wallpaper.SkipTo = null);
            Reload();
        }
        Problem = null;
        try
        {
            Plan = Schedule.Build(Config, Package, Position, now, lockPackage: LockPackage);
            // A color mode set by hand ends when the plan has come to it by itself (or its time is long past): from
            // then on the day goes on as planned.
            if (Config.Appearance.Hold is { } hold && !Plan.ModeHeld)
            {
                ConfigStore.Update(c => c.Appearance.Hold = null);
                Reload();
                Log.Info($"Color mode {(hold.Dark ? "dark" : "light")} reached by the plan");
            }
            Log.Debug($"Resolve ({reason}): image {Plan.Current?.Image.ToString() ?? "-"}, next {Plan.Next?.Start.ToString("dd.MM. HH:mm") ?? "-"}, dark {Plan.Dark?.ToString() ?? "-"}, position {Position?.Source.ToString() ?? "none"}", "Engine");
            var paused = Config.PausedUntil is { } until && until > now;
            // A new installation changes nothing in Windows until the user switched it on at the end of the
            // introduction (kit KitIntro, STANDARD.md "First start"); the plan is shown all the same.
            if ((!paused || byHand) && !DryRun && !KitIntro.Pending)
            {
                // At start the color mode first: a registry value, on screen at once. Otherwise the background first: when it
                // fades (QuickFade), the color mode changes halfway through, so both change together.
                if (reason == "start") ApplyAppearance(reason);
                ApplyWallpaper(reason);
                if (reason != "start")
                {
                    if (fading) modeAfterFade = reason;
                    else ApplyAppearance(reason);
                }
                PrepareGentle();
                ApplyLockScreen(reason);
                ApplySignIn();
            }
            ScheduleNext(paused ? Config.PausedUntil : Plan.NextChange);
            if (reason == "start")
            {
                if (KitIntro.Pending) Log.Info("Not switched on yet: nothing applied at start");
                else LogStart();
            }
        }
        catch (Exception ex)
        {
            Problem = ex.Message;
            Log.Error($"Resolve ({reason}) failed", ex, eventId: Events.ResolveFailed);
            ScheduleNext(now.AddMinutes(1));
        }
        Updated?.Invoke();
    }

    /// <summary>How long it took from the process start to the first plan on screen, so a slow sign-in can be measured.</summary>
    static void LogStart()
    {
        try
        {
            var ms = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
            Log.Info($"Applied at start in {ms:0} ms after the process started");
        }
        catch
        {
            // The start time is not always readable; then there is nothing to measure.
        }
    }

    void ApplyWallpaper(string reason)
    {
        if (!Config.Wallpaper.Enabled || Package == null || Plan?.Current is not { } slot) return;
        if (Wallpaper.Blocked() is { } blocked)
        {
            Problem = blocked;
            return;
        }
        var path = Package.PathOf(Package.Images[slot.Image]);
        if (!File.Exists(path))
        {
            Problem = Strings.T($"The image {Path.GetFileName(path)} is missing.", $"Das Bild {Path.GetFileName(path)} fehlt.");
            return;
        }
        var image = path;
        string? step = null;
        if (Plan.Blend is { } blend && Package.PathOf(Package.Images[blend.To]) is var to && File.Exists(to) && blendFailed != (image, to))
        {
            step = Blend.FramePath(image, to, blend);
            if (!File.Exists(step))
            {
                // Mixed off the window's thread; when it is written, a resolve puts it on screen. Until then the screen
                // keeps what it shows when that is this image or a step of the way; anything else (at sign-in, after
                // another tool) gets the plain image at once instead of waiting for the mix.
                MakeStep(image, to, blend, step);
                var showing = Wallpaper.Current().Values;
                if (showing.Count > 0 && showing.All(p => Wallpaper.SamePath(p, image) || Blend.IsStep(p))) return;
                step = null;
            }
            else path = step;
        }
        var shown = Wallpaper.Current();
        var needed = Config.Wallpaper.Fit == WallpaperFit.Span
            ? shown.Count == 0 || shown.Values.Any(p => !Wallpaper.SamePath(p, path))
            : shown.Values.Any(p => !Wallpaper.SamePath(p, path)) || shown.Count == 0;
        if (!needed) return;

        if (fading) return;
        // From one of ours to the next: faded by DynaTones itself (see QuickFade). Not at start, where the picture
        // comes first and fast, and not over a picture someone else set.
        // Right after a fade Windows may still name its last frame for a moment: that is not a new change.
        var justFaded = fadedTo != null && Wallpaper.SamePath(fadedTo, path) && DateTime.Now - fadedAt < TimeSpan.FromSeconds(10);
        if (step == null && reason != "start" && !justFaded && shown.Values.FirstOrDefault() is { } from && Ours(from) && !Wallpaper.SamePath(from, path)
            && QuickFade(from, path, reason)) return;

        // A different image while ours should be there: someone else set it. Worth a line in the log.
        var foreign = (reason == "check" || reason == "preference") && !shown.Values.All(Blend.IsStep);
        var before = shown.Values.FirstOrDefault(Blend.IsStep);
        if (Wallpaper.Set(path, Config.Wallpaper.Fit, Transitions.Effective(Config.Wallpaper, Package).Fade))
        {
            LastWallpaperChange = DateTime.Now;
            LastReason = reason;
            if (step != null && Plan.Blend is { } b)
            {
                // A line per step would fill the log; the first one says the way has begun.
                var text = $"Wallpaper {Package.Images[b.From].File} to {Package.Images[b.To].File}, step {b.Step} of {b.Steps} ({reason})";
                if (b.Step == 1 || foreign) Log.Info(text);
                else Log.Debug(text, "Engine");
            }
            else
            {
                Log.Info($"Wallpaper {Package.Images[slot.Image].File} ({reason}{(foreign ? ", repaired" : "")})");
            }
            Blend.Clean(step, before);
        }
        else
        {
            Problem = Strings.T("Windows did not take the background. DynaTones tries again in a minute.",
                "Windows hat den Hintergrund nicht übernommen. DynaTones versucht es in einer Minute erneut.");
        }
    }

    bool fading;
    /// <summary>A change of the color mode waiting for the middle of the fade; the reason it came with.</summary>
    string? modeAfterFade;

    void ModeAfterFade()
    {
        if (modeAfterFade is not { } reason) return;
        modeAfterFade = null;
        ApplyAppearance(reason);
    }
    string? fadedTo;
    DateTime fadedAt;

    /// <summary>A picture of DynaTones: an image of a package or a step of a mix, all in its data folder.</summary>
    static bool Ours(string path) =>
        File.Exists(path) && Path.GetFullPath(path).StartsWith(Path.GetFullPath(Paths.Data) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Every change of picture fades, whatever the transition and also by hand (Next image, a jump, back to the plan):
    /// three mixed frames, a quarter, half and three quarters of the way, each shown for about a tenth of a second,
    /// then the picture itself. Windows' own fade cannot be relied on: it follows Windows' animation settings and did
    /// not show at all in Windows Sandbox (measured 09.11.2026, 31 frames a second, none in between). The frames are
    /// mixed first, off the window's thread, at the size of the screens; until they are on screen nothing else changes
    /// the background. False when there is nothing to mix from; then the picture is set as it is.
    /// </summary>
    bool QuickFade(string from, string to, string reason)
    {
        if (!File.Exists(to)) return false;
        fading = true;
        var fit = Config.Wallpaper.Fit;
        var screen = fit == WallpaperFit.Center ? null : Wallpaper.Coverage(fit);
        var frames = Enumerable.Range(1, 3).Select(n => (Path: Blend.FramePath(from, to, new Transition(0, 0, n, QuickSteps, n / 4.0, DateTime.Now)), Amount: n / 4.0)).ToList();
        var watch = Stopwatch.StartNew();
        Task.Run(async () =>
        {
            foreach (var (frame, amount) in frames)
                if (!File.Exists(frame)) await Blend.MakeAsync(from, to, amount, frame, screen);
        }).ContinueWith(done => dispatcher.BeginInvoke(async () =>
        {
            var mixed = watch.ElapsedMilliseconds;
            if (!done.IsFaulted)
            {
                for (var n = 0; n < frames.Count; n++)
                {
                    Wallpaper.Set(frames[n].Path, fit, fade: true);
                    if (n == 1) ModeAfterFade();
                    await Task.Delay(FadeHold);
                }
            }
            else
            {
                Log.Warning($"Fading to {Path.GetFileName(to)} could not be mixed: {done.Exception?.GetBaseException().Message}");
            }
            if (Wallpaper.Set(to, fit, fade: true))
            {
                LastWallpaperChange = DateTime.Now;
                LastReason = reason;
                Log.Info($"Wallpaper {Path.GetFileName(to)} ({reason}, faded{(done.IsFaulted ? " plainly" : $", mixed in {mixed} ms")})");
            }
            ModeAfterFade();
            Blend.Clean(to, frames[^1].Path);
            fadedTo = to;
            fadedAt = DateTime.Now;
            fading = false;
            Later(TimeSpan.FromSeconds(2), "faded");
        }), TaskScheduler.Default);
        return true;
    }

    /// <summary>The frames of <see cref="QuickFade"/> count as steps of four, apart from Gentle's three and Flowing's.</summary>
    const int QuickSteps = 4;

    /// <summary>
    /// How long each frame stands. Windows takes a new background at most about every 0.3 s and skips faster ones: at
    /// 110, 150 and 200 ms none of the frames showed, at 300 ms all of them, in 6 of 6 runs (Windows Sandbox, 09.11.2026).
    /// </summary>
    const int FadeHold = 300;

    string? gentleReady;
    string? signInKey;

    /// <summary>
    /// "Gentle" shows its three steps within seconds, too fast to mix them when they are due: they are mixed in the
    /// minutes before, one after the other, off the window's thread.
    /// </summary>
    void PrepareGentle()
    {
        if (Package is not { } package || Plan?.Current is not { } now || Plan.Next is not { } next || now.Image == next.Image) return;
        var (fade, minutes, seconds) = Transitions.Effective(Config.Wallpaper, package);
        if (!fade || minutes > 0 || seconds <= 0 || next.Start - DateTime.Now > TimeSpan.FromMinutes(3)) return;
        var from = package.PathOf(package.Images[now.Image]);
        var to = package.PathOf(package.Images[next.Image]);
        var key = $"{from}|{to}|{next.Start:O}";
        if (gentleReady == key || !File.Exists(from) || !File.Exists(to)) return;
        gentleReady = key;
        var screen = Config.Wallpaper.Fit == WallpaperFit.Center ? null : Wallpaper.Coverage(Config.Wallpaper.Fit);
        Task.Run(async () =>
        {
            for (var step = 1; step <= 3; step++)
            {
                var t = new Transition(now.Image, next.Image, step, 3, step / 4.0, next.Start);
                var target = Blend.FramePath(from, to, t);
                if (!File.Exists(target)) await Blend.MakeAsync(from, to, t.Amount, target, screen);
            }
        }).ContinueWith(done =>
        {
            if (done.IsFaulted) Log.Warning($"Gentle steps could not be mixed: {done.Exception?.GetBaseException().Message}");
        }, TaskScheduler.Default);
    }

    void MakeStep(string from, string to, Transition blend, string target)
    {
        if (blendBusy) return;
        blendBusy = true;
        var watch = Stopwatch.StartNew();
        Blend.MakeAsync(from, to, blend.Amount, target,
            // Centered shows the original size; there a smaller mix would jump at the end.
            Config.Wallpaper.Fit == WallpaperFit.Center ? null : Wallpaper.Coverage(Config.Wallpaper.Fit)).ContinueWith(done => dispatcher.BeginInvoke(() =>
        {
            blendBusy = false;
            if (done.IsFaulted)
            {
                // Not again for these two images: they change plainly, as without mixing.
                blendFailed = (from, to);
                Log.Error($"Mixing {Path.GetFileName(from)} and {Path.GetFileName(to)} failed", done.Exception?.GetBaseException());
            }
            else
            {
                Log.Debug($"Blend step {blend.Step} of {blend.Steps} made in {watch.ElapsedMilliseconds} ms", "Engine");
            }
            Resolve("blend");
        }), TaskScheduler.Default);
    }

    void ApplyAppearance(string reason)
    {
        var a = Config.Appearance;
        if (!a.Enabled || Plan?.Dark is not { } dark) return;
        bool? Target(ModeTarget t) => t switch
        {
            ModeTarget.Switch => !dark,
            ModeTarget.AlwaysLight => true,
            ModeTarget.AlwaysDark => false,
            _ => null,
        };
        if (Appearance.Apply(Target(a.Apps), Target(a.System)))
            Log.Info($"Mode {(dark ? "dark" : "light")} ({reason})");
        var cursor = dark ? a.CursorDark : a.CursorLight;
        if (!string.IsNullOrEmpty(cursor) && Appearance.ApplyCursorScheme(cursor))
            Log.Info($"Pointer scheme {cursor} ({reason})");
    }

    /// <summary>
    /// The lock screen, through Windows' own interface, which is asynchronous: it runs off the window's thread, one at a
    /// time, and reports back here.
    /// </summary>
    void ApplyLockScreen(string reason)
    {
        if (!Config.LockScreen.Enabled || Plan?.LockPackage is not { } package || Plan.LockCurrent is not { } slot)
        {
            LockProblem = null;
            return;
        }
        if (LockScreen.Blocked() is { } blocked)
        {
            LockProblem = blocked;
            return;
        }
        var path = package.PathOf(package.Images[slot.Image]);
        if (lockBusy || !File.Exists(path) || LockScreen.Shows(path)) return;
        lockBusy = true;
        Task.Run(() => LockScreen.SetAsync(path)).ContinueWith(done => dispatcher.BeginInvoke(() =>
        {
            lockBusy = false;
            LockProblem = done.IsFaulted ? done.Exception?.GetBaseException().Message : done.Result;
            if (LockProblem == null) Log.Info($"Lock screen {Path.GetFileName(path)} ({reason})");
            Updated?.Invoke();
        }), TaskScheduler.Default);
    }

    bool signInBusy;

    /// <summary>
    /// The screen before sign-in follows the lock screen: hands over the lock screen's pictures of the next 36 hours,
    /// so the task at start finds the right one even after the PC was off. Only with the admin tier set up; the task
    /// itself runs only when the plan changed.
    /// </summary>
    void ApplySignIn()
    {
        if (signInBusy || !SignIn.IsInstalled) return;
        // Off, or the lock screen left to Windows: the screen before sign-in goes back to Windows as well.
        if (!Config.LockScreen.Enabled || !Config.LockScreen.SignIn)
        {
            signInKey = null;
            signInBusy = true;
            Task.Run(SignIn.Withdraw).ContinueWith(_ => dispatcher.BeginInvoke(() => signInBusy = false), TaskScheduler.Default);
            return;
        }
        if (Plan?.LockPackage is null) return;
        // The 36 hours ahead change only when the lock screen's pictures, their times or the date do: worked out
        // again only then, not at every check (each time it means up to 40 plans).
        var key = $"{Plan.LockPackage.Id}|{Plan.LockCurrent?.Start:O}|{Plan.LockNext?.Start:O}|{Position?.Latitude:0.00}|{Position?.Longitude:0.00}";
        if (key == signInKey) return;
        signInKey = key;
        var config = Config;
        var package = Package;
        var lockPackage = LockPackage;
        var position = Position;
        signInBusy = true;
        Task.Run(() =>
        {
            var timeline = new List<(DateTime, string)>();
            var at = DateTime.Now;
            var end = at.AddHours(36);
            while (timeline.Count < 40)
            {
                var plan = Schedule.Build(config, package, position, at, lockPackage: lockPackage);
                if (plan.LockPackage is { } p && plan.LockCurrent is { } slot)
                {
                    var path = p.PathOf(p.Images[slot.Image]);
                    // From when the picture shows: stable from one check to the next, so the task runs only on a change.
                    var from = slot.Start;
                    if (File.Exists(path) && (timeline.Count == 0 || timeline[^1].Item2 != path)) timeline.Add((from, path));
                }
                if (plan.LockNext?.Start is not { } next || next > end) break;
                at = next.AddSeconds(1);
            }
            return SignIn.Push(timeline);
        }).ContinueWith(done => dispatcher.BeginInvoke(() => signInBusy = false), TaskScheduler.Default);
    }

    void ScheduleNext(DateTime? at)
    {
        exact.Stop();
        if (at is not { } when) return;
        var wait = when - DateTime.Now + TimeSpan.FromSeconds(1);
        if (wait < TimeSpan.FromSeconds(1)) wait = TimeSpan.FromSeconds(1);
        // Long timers drift over sleep; the minute check catches up, so a cap keeps it simple.
        if (wait > TimeSpan.FromHours(1)) wait = TimeSpan.FromHours(1);
        exact.Interval = wait;
        exact.Start();
    }

    // ---------- Events ----------

    void OnPower(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        // Windows needs a moment after waking before the desktop takes a background.
        Later(TimeSpan.FromSeconds(3), "resume");
        Later(TimeSpan.FromSeconds(30), "resume check");
    }

    void OnSession(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
            Later(TimeSpan.FromSeconds(2), "unlock");
    }

    void OnTime(object? sender, EventArgs e)
    {
        TimeZoneInfo.ClearCachedData();
        Later(TimeSpan.FromSeconds(1), "clock");
    }

    void OnDisplay(object? sender, EventArgs e) => Later(TimeSpan.FromSeconds(2), "display");

    void OnPreference(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
            Later(TimeSpan.FromSeconds(5), "preference");
    }

    void Later(TimeSpan delay, string reason) =>
        Task.Delay(delay).ContinueWith(_ => Resolve(reason), TaskScheduler.Default);

    public void Dispose()
    {
        disposed = true;
        exact.Stop();
        minute.Stop();
        SystemEvents.PowerModeChanged -= OnPower;
        SystemEvents.SessionSwitch -= OnSession;
        SystemEvents.TimeChanged -= OnTime;
        SystemEvents.DisplaySettingsChanged -= OnDisplay;
        SystemEvents.UserPreferenceChanged -= OnPreference;
    }
}
