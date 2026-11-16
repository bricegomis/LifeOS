namespace LifeOS.Domain.Sports;

public enum RoutineDayStatus
{
    /// <summary>Before the start or after the stop.</summary>
    NotCounted,
    /// <summary>Not one of the routine's days: it neither counts nor breaks the streak.</summary>
    NotPlanned,
    Done,
    /// <summary>Missed but covered by a joker of its week: the streak goes on.</summary>
    Joker,
    Missed,
    /// <summary>Today, not finished yet: it never breaks the streak before midnight.</summary>
    InProgress,
}

/// <param name="Done">Exercises done in the best pass of the day.</param>
/// <param name="Passes">Passes finished that day.</param>
public sealed record RoutineDay(DateOnly Date, int Done, int Planned, RoutineDayStatus Status, int Passes = 0)
{
    public int Percent => Planned <= 0 ? 0 : Math.Min(100, Done * 100 / Planned);
}

/// <summary>
/// Streak, record and calendar of a routine, computed on read (ADR 0014, ADR 0015). A planned day is
/// done when one of its passes has every exercise of that day with all its sets; more passes are
/// counted but change nothing else. A missed planned day uses a joker of its week when one is left;
/// otherwise the streak restarts. Days that are not planned are skipped.
/// </summary>
public sealed record RoutineProgress(
    bool PlannedToday,
    int DoneToday,
    int TotalToday,
    int PassesToday,
    int Streak,
    int Record,
    int JokersPerWeek,
    int JokersLeftThisWeek,
    DateOnly? NextPlannedDay,
    IReadOnlyList<RoutineDay> Calendar)
{
    /// <param name="passesByDay">Per day, the sets done per exercise in each pass.</param>
    /// <param name="calendarFrom">First day returned in <see cref="Calendar"/>.</param>
    public static RoutineProgress Compute(Routine routine,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<IReadOnlyDictionary<Guid, int>>> passesByDay, DateOnly today, DateOnly calendarFrom)
    {
        var lastDay = routine.StoppedOn is { } stopped && stopped < today ? stopped : today;
        var days = new Dictionary<DateOnly, RoutineDay>();
        var jokersUsed = new Dictionary<DateOnly, int>();
        int streak = 0, record = 0;
        for (var day = routine.StartedOn; day <= lastDay; day = day.AddDays(1))
        {
            var version = routine.VersionOn(day);
            if (!version.IsPlanned(day))
            {
                days[day] = new RoutineDay(day, 0, 0, RoutineDayStatus.NotPlanned);
                continue;
            }
            var passes = (passesByDay.GetValueOrDefault(day) ?? []).Select(sets => RoutineSession.ExercisesDone(routine, day, sets)).ToList();
            var done = passes.Count == 0 ? 0 : passes.Max();
            var finished = passes.Count(count => count == version.Lines.Count);
            RoutineDayStatus status;
            if (finished > 0) { streak++; status = RoutineDayStatus.Done; }
            else if (day == today) status = RoutineDayStatus.InProgress;
            else
            {
                var monday = SportWeek.Monday(day);
                var used = jokersUsed.GetValueOrDefault(monday);
                if (used < version.JokersPerWeek) { jokersUsed[monday] = used + 1; status = RoutineDayStatus.Joker; }
                else { streak = 0; status = RoutineDayStatus.Missed; }
            }
            record = Math.Max(record, streak);
            days[day] = new RoutineDay(day, done, version.Lines.Count, status, finished);
        }

        var calendar = new List<RoutineDay>();
        for (var day = calendarFrom; day <= today; day = day.AddDays(1))
            calendar.Add(days.GetValueOrDefault(day) ?? new RoutineDay(day, 0, 0, RoutineDayStatus.NotCounted));

        var running = routine.IsActive || routine.StoppedOn >= today;
        var current = routine.VersionOn(today);
        var todayDay = running ? days.GetValueOrDefault(today) : null;
        DateOnly? next = null;
        if (routine.IsActive)
            next = Enumerable.Range(1, 7).Select(today.AddDays).First(current.IsPlanned);
        return new RoutineProgress(
            PlannedToday: todayDay is { Status: not RoutineDayStatus.NotPlanned },
            DoneToday: todayDay?.Done ?? 0,
            TotalToday: todayDay?.Planned ?? 0,
            PassesToday: todayDay?.Passes ?? 0,
            Streak: running ? streak : 0,
            Record: record,
            JokersPerWeek: current.JokersPerWeek,
            JokersLeftThisWeek: Math.Max(0, current.JokersPerWeek - jokersUsed.GetValueOrDefault(SportWeek.Monday(today))),
            NextPlannedDay: next,
            Calendar: calendar);
    }
}

/// <summary>Best value and best load of an exercise in one week; null when it was not done.</summary>
public sealed record ExerciseWeek(DateOnly Monday, int? BestValue, decimal? BestLoadKg);

/// <summary>
/// Follow-up of one exercise (ADR 0014): its best value and best load per week and since the
/// start, all routines and passes together. Value and load are never combined into a score.
/// </summary>
/// <param name="Recent">Latest ticks first, with their feeling (ADR 0015).</param>
public sealed record ExerciseProgress(int? BestValue, decimal? BestLoadKg, int DaysDone, IReadOnlyList<ExerciseWeek> Weeks,
    IReadOnlyList<ExerciseCompletion> Recent)
{
    public const int RecentCount = 10;

    public static ExerciseProgress Compute(IReadOnlyCollection<ExerciseCompletion> completions, DateOnly today, int weeks)
    {
        var firstMonday = SportWeek.Monday(today).AddDays(-7 * (weeks - 1));
        var byWeek = completions.Where(c => c.Date >= firstMonday).ToLookup(c => SportWeek.Monday(c.Date));
        return new ExerciseProgress(
            completions.Count == 0 ? null : completions.Max(c => c.Value),
            completions.Max(c => c.LoadKg),
            completions.Select(c => c.Date).Distinct().Count(),
            Enumerable.Range(0, weeks).Select(i => firstMonday.AddDays(7 * i))
                .Select(monday => new ExerciseWeek(monday,
                    byWeek[monday].Any() ? byWeek[monday].Max(c => c.Value) : null,
                    byWeek[monday].Max(c => c.LoadKg)))
                .ToList(),
            completions.OrderByDescending(c => c.Date).ThenByDescending(c => c.LoggedAt).Take(RecentCount).ToList());
    }
}
