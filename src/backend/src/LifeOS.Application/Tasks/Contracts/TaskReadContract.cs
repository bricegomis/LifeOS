using LifeOS.Domain.Tasks;

namespace LifeOS.Application.Tasks.Contracts;

/// <summary>Reads the Tasks read contract needs; implemented by Infrastructure on the tasks tables.</summary>
public interface ITaskReadData
{
    Task<IReadOnlyList<Todo>> OpenTodosAsync(Guid householdId, CancellationToken ct);
    /// <summary>Todos done from <paramref name="from"/> to <paramref name="to"/> included.</summary>
    Task<IReadOnlyList<Todo>> DoneTodosAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct);
    /// <summary>Routines with their passes; stopped and paused ones included unless <paramref name="activeOnly"/>.</summary>
    Task<IReadOnlyList<TaskRoutine>> RoutinesAsync(Guid householdId, bool activeOnly, CancellationToken ct);
}

/// <param name="IsLate">Open with a due day before today (Paris).</param>
public sealed record TaskOpenTodoDto(Guid Id, Guid MemberProfileId, string Title, DateOnly? DueOn, bool IsLate, int? DurationMinutes,
    string Priority, Guid? ProjectId);

/// <param name="IsOpen">The routine's open due day; the next ones are projected as if each were done on its day.</param>
/// <param name="IsLate">The open due day is before today.</param>
public sealed record TaskRoutineDueDto(Guid RoutineId, Guid MemberProfileId, string Title, DateOnly DueOn, bool IsOpen, bool IsLate,
    int? DurationMinutes, string Priority);

/// <param name="Kind">todo or routine.</param>
/// <param name="Id">The todo, or the routine of the pass.</param>
public sealed record TaskDoneDto(string Kind, Guid Id, string Title, int? DurationMinutes);

/// <param name="DurationMinutes">Sum of the estimated durations that are known.</param>
/// <param name="DurationComplete">Every task done that day has an estimated duration.</param>
public sealed record TaskDayDto(DateOnly Date, Guid MemberProfileId, IReadOnlyList<TaskDoneDto> Done, int DurationMinutes,
    bool DurationComplete);

/// <summary>
/// Published read contract of the Tasks module (ADR 0009, ADR 0017), for the future planner: open todos,
/// due days of the routines over a period and what was done per day and per profile, all computed on read.
/// </summary>
public sealed class TaskReadContract(ITaskReadData data, TimeProvider clock)
{
    private const int MaxDays = 366;

    public DateOnly Today() => TaskClock.Today(clock);

    /// <summary>Every open todo of the household, late ones first, then by due day (none last), priority and title.</summary>
    public async Task<IReadOnlyList<TaskOpenTodoDto>> OpenTodosAsync(Guid householdId, CancellationToken ct)
    {
        var today = Today();
        return (await data.OpenTodosAsync(householdId, ct))
            .OrderBy(t => t.DueOn is null).ThenBy(t => t.DueOn).ThenBy(t => PriorityRank(t.Priority)).ThenBy(t => t.Title)
            .Select(t => new TaskOpenTodoDto(t.Id, t.MemberProfileId, t.Title, t.DueOn, t.IsLate(today), t.DurationMinutes, t.Priority, t.ProjectId))
            .ToList();
    }

    /// <summary>Due days of the active routines up to <paramref name="to"/>: the open one (late ones included), then the projected ones.</summary>
    public async Task<IReadOnlyList<TaskRoutineDueDto>> RoutineDuesAsync(Guid householdId, DateOnly to, CancellationToken ct)
    {
        var today = Today();
        if (to < today || to.DayNumber - today.DayNumber > MaxDays) throw new ArgumentException($"Période : aujourd'hui à {MaxDays} jours.");
        return (await data.RoutinesAsync(householdId, true, ct))
            .SelectMany(r => RoutineSchedule.DueDays(r, today, to).Select((day, i) =>
                new TaskRoutineDueDto(r.Id, r.MemberProfileId, r.Title, day, i == 0, i == 0 && day < today, r.DurationMinutes, r.Priority)))
            .OrderBy(d => d.DueOn).ThenBy(d => PriorityRank(d.Priority)).ThenBy(d => d.Title)
            .ToList();
    }

    /// <summary>Todos done and routine passes done, per day and per profile; skipped passes are not counted.</summary>
    public async Task<IReadOnlyList<TaskDayDto>> DaysAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxDays) throw new ArgumentException($"Période : 1 à {MaxDays} jours.");
        var todos = (await data.DoneTodosAsync(householdId, from, to, ct))
            .Select(t => (Day: t.DoneOn!.Value, t.MemberProfileId, Done: new TaskDoneDto("todo", t.Id, t.Title, t.DurationMinutes)));
        var passes = (await data.RoutinesAsync(householdId, false, ct))
            .SelectMany(r => r.Passes.Where(p => p.Kind == "done" && p.Day >= from && p.Day <= to)
                .Select(p => (p.Day, r.MemberProfileId, Done: new TaskDoneDto("routine", r.Id, r.Title, r.DurationMinutes))));
        return todos.Concat(passes)
            .GroupBy(x => (x.Day, x.MemberProfileId)).OrderBy(g => g.Key.Day).ThenBy(g => g.Key.MemberProfileId)
            .Select(g => new TaskDayDto(g.Key.Day, g.Key.MemberProfileId, g.Select(x => x.Done).ToList(),
                g.Sum(x => x.Done.DurationMinutes ?? 0), g.All(x => x.Done.DurationMinutes is not null)))
            .ToList();
    }

    private static int PriorityRank(string priority) => priority switch { "high" => 0, "normal" => 1, _ => 2 };
}
