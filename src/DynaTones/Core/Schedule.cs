namespace DynaTones.Core;

/// <summary>
/// What should be on screen at a moment, and when that changes next. A pure calculation from the settings, the
/// package and the position: the same input always gives the same answer, which is what lets the engine compare
/// what should be with what is, as often as it likes.
/// </summary>
public sealed class Plan
{
    public DateTime At { get; init; }
    public SunDay? Today { get; init; }
    /// <summary>The images of today, for the timeline.</summary>
    public IReadOnlyList<Slot> TodaySlots { get; init; } = [];
    public Slot? Current { get; init; }
    public Slot? Next { get; init; }
    /// <summary>Set while "Next image" is in effect: the slot shown ahead of its time (also <see cref="Current"/>).</summary>
    public Slot? Skipped { get; init; }
    /// <summary>Null when light and dark are not switched.</summary>
    public bool? Dark { get; init; }
    public DateTime? NextModeChange { get; init; }
    /// <summary>The lock screen: its package (the wallpaper's or its own) and the image now and next; null when off.</summary>
    public Package? LockPackage { get; init; }
    public Slot? LockCurrent { get; init; }
    public Slot? LockNext { get; init; }
    /// <summary>Today's images of the lock screen's own package, for the timeline; empty when it shows the desktop's.</summary>
    public IReadOnlyList<Slot> LockTodaySlots { get; init; } = [];
    /// <summary>Today's switches between light and dark, for the timeline.</summary>
    public IReadOnlyList<(DateTime At, bool Dark)> TodayModes { get; init; } = [];

    /// <summary>The mix of the current and the next image shown now, or null when an image is shown as it is.</summary>
    public Transition? Blend { get; init; }

    /// <summary>When the engine has to act next, steps of a mix included.</summary>
    public DateTime? NextChange => new[] { NextAnnounced, Blend?.NextStep, BlendBegins }.Where(t => t != null).Min();

    /// <summary>When the mix before the next image begins, while it has not begun: the engine wakes for it.</summary>
    public DateTime? BlendBegins { get; init; }

    /// <summary>The next change worth telling: a new image, the color mode or the lock screen, not a step of a mix.</summary>
    public DateTime? NextAnnounced => new[] { Next?.Start, NextModeChange, LockNext?.Start }.Where(t => t != null).Min();
}

/// <summary>
/// One step of the way from one image to the next: <see cref="Amount"/> of <see cref="To"/> over <see cref="From"/>,
/// until <see cref="NextStep"/>.
/// </summary>
public sealed record Transition(int From, int To, int Step, int Steps, double Amount, DateTime NextStep);

public static class Schedule
{
    /// <param name="lockPackage">The lock screen's own package, when it has one (<see cref="LockScreenSource.Package"/>).</param>
    public static Plan Build(Config config, Package? package, Position? position, DateTime now, TimeZoneInfo? zone = null, Package? lockPackage = null)
    {
        zone ??= TimeZoneInfo.Local;
        // A package without pictures yet (just made by hand) shows nothing.
        if (package?.Images.Count == 0) package = null;
        if (lockPackage?.Images.Count == 0) lockPackage = null;
        var place = position ?? Locator.FromTimeZone() ?? new Position(50, zone.BaseUtcOffset.TotalHours * 15, LocationSource.TimeZone, "");
        var date = DateOnly.FromDateTime(now);
        var days = new[] { date.AddDays(-1), date, date.AddDays(1) }
            .Select(d => new SunDay(d, place.Latitude, place.Longitude, zone)).ToArray();

        // Images: three days in a row, so the image of the night before and the first one tomorrow are known.
        var slots = new List<Slot>();
        IReadOnlyList<Slot> todaySlots = [];
        if (package != null && config.Wallpaper.Enabled)
        {
            foreach (var day in days)
            {
                var list = Planner.Day(package, day);
                if (day.Date == date) todaySlots = list;
                slots.AddRange(list);
            }
        }
        var current = slots.LastOrDefault(s => s.Start <= now);
        var next = slots.FirstOrDefault(s => s.Start > now);
        // "Next image": the image of the coming slot already now, and the plan one step ahead until that slot's own
        // start. Matched within 20 minutes, as the slot moves a little when the position is refined.
        Slot? skipped = null;
        if (config.Wallpaper.SkipTo is { } skipTo
            && slots.FirstOrDefault(s => s.Start > now && Math.Abs((s.Start - skipTo).TotalMinutes) < 20) is { } target)
        {
            skipped = target;
            current = target;
            next = slots.FirstOrDefault(s => s.Start > target.Start);
        }
        Transition? blend = null;
        var (fade, minutes, seconds) = Transitions.Effective(config.Wallpaper, package);
        if (fade && current != null && next != null && current.Image != next.Image)
            blend = minutes > 0
                ? Between(current.Start, next.Start, current.Image, next.Image, minutes, now)
                : seconds > 0 ? Gently(current.Start, next.Start, current.Image, next.Image, seconds, now) : null;
        DateTime? blendBegins = null;
        if (fade && blend == null && next != null && current != null && current.Image != next.Image && (minutes > 0 || seconds > 0))
        {
            var begins = next.Start - (minutes > 0 ? TimeSpan.FromMinutes(Math.Min(minutes, (next.Start - current.Start).TotalMinutes))
                : TimeSpan.FromSeconds(Math.Min(seconds, (next.Start - current.Start).TotalSeconds / 2)));
            if (begins > now) blendBegins = begins;
        }

        // Light and dark.
        var a = config.Appearance;
        bool? dark = null;
        DateTime? nextMode = null;
        var todayModes = new List<(DateTime, bool)>();
        if (a.Enabled)
        {
            var events = new List<(DateTime At, bool Dark)>();
            foreach (var day in days)
            {
                var list = ModeEvents(a, day, package != null && config.Wallpaper.Enabled ? Planner.Day(package, day) : []);
                if (day.Date == date) todayModes.AddRange(list);
                events.AddRange(list);
            }
            events.Sort((x, y) => x.At.CompareTo(y.At));
            var last = events.LastOrDefault(e => e.At <= now);
            dark = last == default ? events.FirstOrDefault().Dark : last.Dark;
            // The next moment the mode really changes, skipping events that repeat the current one.
            var state = dark;
            foreach (var e in events.Where(e => e.At > now))
            {
                if (e.Dark == state) continue;
                nextMode = e.At;
                break;
            }
        }

        // The lock screen: the wallpaper's images at the same moments, or a package of its own, placed the same way.
        var lockShown = config.LockScreen.Source == LockScreenSource.Package ? lockPackage : package;
        Slot? lockCurrent = null, lockNext = null;
        IReadOnlyList<Slot> lockToday = [];
        if (config.LockScreen.Enabled && lockShown != null)
        {
            var lockSlots = days.SelectMany(day => Planner.Day(lockShown, day)).ToList();
            if (!ReferenceEquals(lockShown, package)) lockToday = Planner.Day(lockShown, days[1]);
            lockCurrent = lockSlots.LastOrDefault(s => s.Start <= now);
            lockNext = lockSlots.FirstOrDefault(s => s.Start > now);
            // The desktop's images at the same moments: a skip there is a skip here too.
            if (skipped != null && ReferenceEquals(lockShown, package))
            {
                lockCurrent = current;
                lockNext = next;
            }
        }

        return new Plan
        {
            LockPackage = config.LockScreen.Enabled ? lockShown : null,
            LockCurrent = lockCurrent,
            LockNext = lockNext,
            LockTodaySlots = lockToday,
            At = now,
            Today = days[1],
            TodaySlots = todaySlots,
            Current = current,
            Next = next,
            Skipped = skipped,
            Dark = dark,
            NextModeChange = nextMode,
            TodayModes = todayModes,
            Blend = blend,
            BlendBegins = blendBegins,
        };
    }

    /// <summary>
    /// The step of the mix at <paramref name="now"/>, or null outside it. The mix takes the last
    /// <paramref name="minutes"/> before the next image starts, at most the whole time the current one is shown, in steps
    /// of about a minute (at most 30); each step shows the mix of its middle, so the way starts just above the current
    /// image and ends just below the next one. Under two minutes it is not worth it.
    /// </summary>
    /// <summary>"Gentle": three steps in the last few seconds before the next image, each faded in by Windows.</summary>
    public static Transition? Gently(DateTime start, DateTime end, int from, int to, int seconds, DateTime now)
    {
        var window = TimeSpan.FromSeconds(Math.Min(seconds, (end - start).TotalSeconds / 2));
        if (window < TimeSpan.FromSeconds(2)) return null;
        var begin = end - window;
        if (now < begin || now >= end) return null;
        const int steps = 3;
        var length = window / steps;
        var step = Math.Min(steps, (int)((now - begin) / length) + 1);
        return new Transition(from, to, step, steps, step / (double)(steps + 1), step == steps ? end : begin + length * step);
    }

    public static Transition? Between(DateTime start, DateTime end, int from, int to, int minutes, DateTime now)
    {
        if (minutes <= 0) return null;
        var window = TimeSpan.FromMinutes(Math.Min(minutes, (end - start).TotalMinutes));
        if (window < TimeSpan.FromMinutes(2)) return null;
        var begin = end - window;
        if (now < begin || now >= end) return null;
        var steps = Math.Clamp((int)Math.Round(window.TotalMinutes), 2, 30);
        var length = window / steps;
        var step = Math.Min(steps, (int)((now - begin) / length) + 1);
        return new Transition(from, to, step, steps, (step - 0.5) / steps, step == steps ? end : begin + length * step);
    }

    static List<(DateTime At, bool Dark)> ModeEvents(AppearanceSettings a, SunDay day, IReadOnlyList<Slot> slots)
    {
        var list = new List<(DateTime, bool)>();
        switch (a.Trigger)
        {
            case AppearanceTrigger.Times:
                if (Planner.TryTime(a.LightFrom, out var light)) list.Add((day.Date.ToDateTime(light), false));
                if (Planner.TryTime(a.DarkFrom, out var darkFrom)) list.Add((day.Date.ToDateTime(darkFrom), true));
                break;
            case AppearanceTrigger.Image when slots.Count > 0:
                foreach (var s in slots) list.Add((s.Start, s.Arc < 0));
                break;
            default:
                var sunrise = day.Crossing(Sun.SunriseElevation, rising: true);
                var sunset = day.Crossing(Sun.SunriseElevation, rising: false);
                if (sunrise is { } up) list.Add((up.AddMinutes(a.SunriseOffsetMinutes), false));
                if (sunset is { } down) list.Add((down.AddMinutes(a.SunsetOffsetMinutes), true));
                // Polar day or night: one state for the whole day.
                if (sunrise == null && sunset == null) list.Add((day.Start, day.Highest < Sun.SunriseElevation));
                break;
        }
        return list.OrderBy(e => e.Item1).ToList();
    }
}
