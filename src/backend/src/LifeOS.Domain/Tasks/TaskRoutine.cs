using LifeOS.Domain.Common;

namespace LifeOS.Domain.Tasks;

/// <summary>
/// Something to do again and again in the household (ADR 0017): take the bins out, change the
/// sheets, pay the canteen. It recurs on fixed dates or a delay after the last time, has one open
/// due day at a time (late ones never pile up), and keeps every pass: done (day, note) or skipped.
/// The next due day, the streak and the record are computed on read by <see cref="RoutineSchedule"/>.
/// </summary>
public sealed class TaskRoutine : Entity
{
    public static readonly IReadOnlyDictionary<string, string> States = new Dictionary<string, string>
    {
        ["active"] = "Active",
        ["paused"] = "En pause",
        ["stopped"] = "Arrêtée",
    };

    public const int MaxNoteLength = 500;

    private readonly List<RoutinePass> _passes = [];

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Notes { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string Priority { get; private set; } = "normal";
    public List<string> Tags { get; private set; } = [];
    public Recurrence Recurrence { get; private set; } = null!;
    /// <summary>The first possible due day; fixed dates are counted from it.</summary>
    public DateOnly StartOn { get; private set; }
    /// <summary>The day it last resumed after a pause: no due day before it, so a pause never makes it late.</summary>
    public DateOnly? ResumedOn { get; private set; }
    public string State { get; private set; } = "active";
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<RoutinePass> Passes => _passes;
    public bool HasImage => ImagePath is not null;
    public bool IsActive => State == "active";

    private TaskRoutine() { }

    public static TaskRoutine Create(Guid householdId, Guid memberProfileId, string title, string? notes, int? durationMinutes,
        string? priority, IEnumerable<string>? tags, Recurrence recurrence, DateOnly startOn, DateTimeOffset now)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        if (memberProfileId == Guid.Empty) throw new ArgumentException("Profil obligatoire.");
        var routine = new TaskRoutine { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId, CreatedAt = now };
        routine.Update(title, notes, durationMinutes, priority, tags, recurrence, startOn, now);
        return routine;
    }

    /// <summary>A new recurrence applies to the next due days; past passes do not change.</summary>
    public void Update(string title, string? notes, int? durationMinutes, string? priority, IEnumerable<string>? tags,
        Recurrence recurrence, DateOnly startOn, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > Todo.MaxTitleLength)
            throw new ArgumentException($"Titre obligatoire ({Todo.MaxTitleLength} caractères maximum).");
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text?.Length > Todo.MaxNotesLength) throw new ArgumentException($"Notes : {Todo.MaxNotesLength} caractères maximum.");
        if (durationMinutes is < 1 or > Todo.MaxDurationMinutes)
            throw new ArgumentException($"Durée estimée : de 1 à {Todo.MaxDurationMinutes} minutes.");
        var code = string.IsNullOrWhiteSpace(priority) ? "normal" : priority.Trim();
        if (!Todo.Priorities.ContainsKey(code)) throw new ArgumentException("Priorité : high, normal ou low.");
        Title = title.Trim();
        Notes = text;
        DurationMinutes = durationMinutes;
        Priority = code;
        Tags = Todo.CleanTags(tags);
        Recurrence = recurrence ?? throw new ArgumentException("Récurrence obligatoire.");
        StartOn = startOn;
        UpdatedAt = now;
    }

    /// <summary>Closes the open due day: done on <paramref name="day"/> (today or before), with an optional note.</summary>
    public RoutinePass Complete(DateOnly day, string? note, DateOnly today, DateTimeOffset now)
    {
        var due = OpenDue(today);
        if (day > today) throw new ArgumentException("Un passage ne peut pas être dans le futur.");
        if (_passes.Count > 0 && day < _passes.Max(p => p.Day))
            throw new ArgumentException("Ce jour est avant le dernier passage.");
        var pass = new RoutinePass(Guid.NewGuid(), "done", day, due, note);
        _passes.Add(pass);
        UpdatedAt = now;
        return pass;
    }

    /// <summary>Skips the open due day; the streak starts again from zero.</summary>
    public RoutinePass Skip(DateOnly today, DateTimeOffset now)
    {
        var due = OpenDue(today);
        var pass = new RoutinePass(Guid.NewGuid(), "skipped", today, due, null);
        _passes.Add(pass);
        UpdatedAt = now;
        return pass;
    }

    /// <summary>Removes a pass; the open due day is computed again.</summary>
    public void RemovePass(Guid passId, DateTimeOffset now)
    {
        var pass = _passes.SingleOrDefault(p => p.Id == passId) ?? throw new ArgumentException("Passage introuvable.");
        _passes.Remove(pass);
        UpdatedAt = now;
    }

    /// <summary>No due day while paused; the streak is kept.</summary>
    public void Pause(DateTimeOffset now)
    {
        if (!IsActive) throw new ArgumentException("Seule une routine active peut être mise en pause.");
        State = "paused";
        UpdatedAt = now;
    }

    /// <summary>Starts again from today: the time spent in pause never makes it late.</summary>
    public void Resume(DateOnly today, DateTimeOffset now)
    {
        if (State != "paused") throw new ArgumentException("Seule une routine en pause peut reprendre.");
        State = "active";
        ResumedOn = today;
        UpdatedAt = now;
    }

    /// <summary>Stopped for good; the history stays.</summary>
    public void Stop(DateTimeOffset now)
    {
        if (State == "stopped") throw new ArgumentException("Cette routine est déjà arrêtée.");
        State = "stopped";
        UpdatedAt = now;
    }

    public void SetImage(string imagePath, string imageContentType)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || imagePath.Length > 500) throw new ArgumentException("Référence d'image invalide.");
        if (imageContentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Format d'image invalide.");
        ImagePath = imagePath;
        ImageContentType = imageContentType;
    }

    public void RemoveImage()
    {
        ImagePath = null;
        ImageContentType = null;
    }

    private DateOnly OpenDue(DateOnly today)
    {
        if (!IsActive) throw new ArgumentException("Cette routine n'est pas active.");
        return RoutineSchedule.Compute(this, today).NextDueOn!.Value;
    }
}

/// <summary>One pass of a routine: done on <see cref="Day"/>, or skipped that day; it closed the due day <see cref="DueOn"/>.</summary>
public sealed class RoutinePass
{
    public Guid Id { get; private set; }
    /// <summary><c>done</c> or <c>skipped</c>.</summary>
    public string Kind { get; private set; } = "done";
    public DateOnly Day { get; private set; }
    public DateOnly DueOn { get; private set; }
    public string? Note { get; private set; }
    /// <summary>Done at the latest on its due day: it keeps the streak going.</summary>
    public bool OnTime => Kind == "done" && Day <= DueOn;

    private RoutinePass() { }

    internal RoutinePass(Guid id, string kind, DateOnly day, DateOnly dueOn, string? note)
    {
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text?.Length > TaskRoutine.MaxNoteLength) throw new ArgumentException($"Note : {TaskRoutine.MaxNoteLength} caractères maximum.");
        Id = id;
        Kind = kind;
        Day = day;
        DueOn = dueOn;
        Note = text;
    }
}

/// <summary>
/// How a routine recurs (ADR 0017). <c>fixed</c>: every N days, on some days of the week every N
/// weeks, or on a day of the month every N months (the 31st falls on the last day of a shorter
/// month). <c>delay</c>: N days after the last pass.
/// </summary>
public sealed record Recurrence
{
    public string Kind { get; private init; } = "fixed";
    /// <summary><c>day</c>, <c>week</c> or <c>month</c>, for a fixed recurrence.</summary>
    public string Unit { get; private init; } = "week";
    public int Every { get; private init; } = 1;
    /// <summary>Days of the week as bits, Monday = 1 … Sunday = 64, for a weekly recurrence.</summary>
    public int Days { get; private init; }
    public int MonthDay { get; private init; } = 1;
    /// <summary>Days after the last pass, for a delay recurrence.</summary>
    public int DelayDays { get; private init; } = 7;

    private Recurrence() { }

    public static Recurrence FixedDays(int every) => Fixed("day", every, [], 1);
    public static Recurrence Weekly(int every, IEnumerable<int> days) => Fixed("week", every, days, 1);
    public static Recurrence Monthly(int every, int monthDay) => Fixed("month", every, [], monthDay);

    /// <param name="days">Days of the week, 1 (Monday) to 7 (Sunday), for a weekly recurrence.</param>
    public static Recurrence Fixed(string unit, int every, IEnumerable<int>? days, int monthDay)
    {
        if (unit is not ("day" or "week" or "month")) throw new ArgumentException("Unité : day, week ou month.");
        if (every is < 1 or > 365) throw new ArgumentException("Tous les … : de 1 à 365.");
        var bits = 0;
        if (unit == "week")
        {
            foreach (var day in days ?? [])
            {
                if (day is < 1 or > 7) throw new ArgumentException("Les jours de la semaine vont de 1 (lundi) à 7 (dimanche).");
                bits |= 1 << (day - 1);
            }
            if (bits == 0) throw new ArgumentException("Choisissez au moins un jour de la semaine.");
        }
        if (unit == "month" && monthDay is < 1 or > 31) throw new ArgumentException("Jour du mois : de 1 à 31.");
        return new Recurrence { Kind = "fixed", Unit = unit, Every = every, Days = bits, MonthDay = unit == "month" ? monthDay : 1 };
    }

    public static Recurrence Delay(int days)
    {
        if (days is < 1 or > 365) throw new ArgumentException("Délai : de 1 à 365 jours.");
        return new Recurrence { Kind = "delay", Unit = "day", Every = 1, DelayDays = days };
    }

    public IReadOnlyList<int> WeekDays => Enumerable.Range(1, 7).Where(day => (Days & (1 << (day - 1))) != 0).ToList();

    /// <summary>The first date of a fixed recurrence on or after <paramref name="from"/>, counted from <paramref name="anchor"/>.</summary>
    public DateOnly FirstOnOrAfter(DateOnly from, DateOnly anchor)
    {
        if (Kind != "fixed") throw new InvalidOperationException("Only a fixed recurrence has dates.");
        if (from < anchor) from = anchor;
        switch (Unit)
        {
            case "day":
            {
                var gap = from.DayNumber - anchor.DayNumber;
                var steps = (gap + Every - 1) / Every;
                return anchor.AddDays(steps * Every);
            }
            case "week":
            {
                var anchorMonday = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
                for (var day = from; day < from.AddDays(7 * Every + 7); day = day.AddDays(1))
                {
                    var week = (day.DayNumber - anchorMonday.DayNumber) / 7;
                    var weekday = ((int)day.DayOfWeek + 6) % 7;
                    if (week % Every == 0 && (Days & (1 << weekday)) != 0) return day;
                }
                throw new InvalidOperationException("A weekly recurrence always has a date within its period.");
            }
            default:
            {
                var month = anchor.Year * 12 + anchor.Month - 1;
                var fromMonth = from.Year * 12 + from.Month - 1;
                if (fromMonth > month) month += (fromMonth - month) / Every * Every;
                while (true)
                {
                    var year = month / 12;
                    var m = month % 12 + 1;
                    var date = new DateOnly(year, m, Math.Min(MonthDay, DateTime.DaysInMonth(year, m)));
                    if (date >= from) return date;
                    month += Every;
                }
            }
        }
    }
}

/// <summary>What a routine looks like on a day (ADR 0017), computed from its recurrence and passes.</summary>
/// <param name="NextDueOn">The open due day; none while paused or stopped.</param>
/// <param name="Streak">Consecutive due days done at the latest on the day; a late or skipped one resets it.</param>
public sealed record RoutineStatus(DateOnly? NextDueOn, bool IsLate, int Streak, int Record, DateOnly? LastDoneOn, bool DoneToday);

public static class RoutineSchedule
{
    public static RoutineStatus Compute(TaskRoutine routine, DateOnly today)
    {
        var passes = routine.Passes.OrderBy(p => p.DueOn).ThenBy(p => p.Day).ToList();
        int streak = 0, record = 0;
        foreach (var pass in passes)
        {
            streak = pass.OnTime ? streak + 1 : 0;
            record = Math.Max(record, streak);
        }
        var done = passes.Where(p => p.Kind == "done").Select(p => (DateOnly?)p.Day).Max();
        DateOnly? next = routine.IsActive ? NextDue(routine, passes.LastOrDefault()) : null;
        return new RoutineStatus(next, next < today, streak, record, done, done == today);
    }

    /// <summary>
    /// The due days of a routine up to <paramref name="to"/>, for the planner: the open one (late ones
    /// included), then the next ones as if each were done on its day (a late open one, today). None while
    /// paused or stopped.
    /// </summary>
    public static IReadOnlyList<DateOnly> DueDays(TaskRoutine routine, DateOnly today, DateOnly to)
    {
        if (Compute(routine, today).NextDueOn is not { } due || due > to) return [];
        var days = new List<DateOnly> { due };
        var r = routine.Recurrence;
        var doneOn = Max(due, today);
        while (true)
        {
            var next = r.Kind == "delay" ? doneOn.AddDays(r.DelayDays) : r.FirstOnOrAfter(doneOn.AddDays(1), routine.StartOn);
            if (next > to) return days;
            days.Add(next);
            doneOn = next;
        }
    }

    private static DateOnly NextDue(TaskRoutine routine, RoutinePass? last)
    {
        var r = routine.Recurrence;
        var notBefore = routine.ResumedOn is { } resumed && resumed > routine.StartOn ? resumed : routine.StartOn;
        if (r.Kind == "delay")
        {
            var after = last is null ? routine.StartOn : last.Day.AddDays(r.DelayDays);
            return Max(after, notBefore);
        }
        // Fixed dates: the first date after the closed due day and after the pass itself, so late
        // due days never pile up.
        var from = last is null ? notBefore : Max(Max(last.DueOn, last.Day).AddDays(1), notBefore);
        return r.FirstOnOrAfter(from, routine.StartOn);
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
