using LifeOS.Domain.Tasks;

namespace LifeOS.Application.Tasks.Contracts;

public sealed record ChecklistItemDto(Guid Id, string Label, bool Done);

/// <param name="State">open, done or abandoned.</param>
/// <param name="IsLate">Open with a due day before today (Paris).</param>
/// <param name="ProjectTitle">Name of its project, when it has one.</param>
public sealed record TodoDto(Guid Id, Guid MemberProfileId, string Title, string? Notes, DateOnly? DueOn, int? DurationMinutes,
    string Priority, IReadOnlyList<string> Tags, string State, DateOnly? DoneOn, bool IsLate, bool HasImage,
    IReadOnlyList<ChecklistItemDto> Checklist, DateTimeOffset UpdatedAt, Guid? ProjectId, string? ProjectTitle)
{
    public static TodoDto From(Todo t, DateOnly today, string? projectTitle = null) =>
        new(t.Id, t.MemberProfileId, t.Title, t.Notes, t.DueOn, t.DurationMinutes, t.Priority, t.Tags, t.State, t.DoneOn,
            t.IsLate(today), t.HasImage, t.OrderedChecklist.Select(i => new ChecklistItemDto(i.Id, i.Label, i.Done)).ToList(),
            t.UpdatedAt, t.ProjectId, t.ProjectId is null ? null : projectTitle);
}

/// <param name="State">active, done or abandoned.</param>
/// <param name="ParentId">Its parent project, when it is a sub-project.</param>
/// <param name="Done">Done todos of the project and of all its sub-projects.</param>
/// <param name="Total">Todos of the project and of all its sub-projects that are not abandoned: progress is
/// <paramref name="Done"/> / <paramref name="Total"/>.</param>
/// <param name="SubProjects">Its direct sub-projects in progress.</param>
public sealed record TaskProjectDto(Guid Id, string Title, string? Notes, DateOnly? DueOn, string State, bool HasImage,
    DateTimeOffset UpdatedAt, int Done, int Total, Guid? ParentId = null, int SubProjects = 0)
{
    public int Open => Total - Done;

    /// <param name="todos">The todos of the project and of all its sub-projects.</param>
    public static TaskProjectDto From(TaskProject p, IEnumerable<Todo> todos, int subProjects = 0)
    {
        var counted = todos.Where(t => t.State != "abandoned").ToList();
        return new(p.Id, p.Title, p.Notes, p.DueOn, p.State, p.HasImage, p.UpdatedAt, counted.Count(t => t.State == "done"), counted.Count,
            p.ParentId, subProjects);
    }
}

public sealed record TaskProjectRefDto(Guid Id, string Title);

/// <summary>
/// A project with its own todos (open first by due day, then done and abandoned), its direct
/// sub-projects (in progress first) and its ancestors, from the top project down to its parent.
/// </summary>
public sealed record TaskProjectDetailDto(TaskProjectDto Project, IReadOnlyList<TodoDto> Todos,
    IReadOnlyList<TaskProjectDto> SubProjects, IReadOnlyList<TaskProjectRefDto> Ancestors);

/// <summary>
/// The "Aujourd'hui" page (ADR 0017): open todos due up to <paramref name="Today"/> + 7 days (late ones
/// included), open todos without a due day, and those done today; active routines due within the
/// same week or done today.
/// </summary>
public sealed record TaskTodayDto(DateOnly Today, IReadOnlyList<TodoDto> Todos, IReadOnlyList<TaskRoutineDto> Routines);

/// <param name="Kind">fixed or delay.</param>
/// <param name="Unit">day, week or month, for fixed dates.</param>
/// <param name="Days">Days of the week, 1 (Monday) to 7 (Sunday), for a weekly recurrence.</param>
/// <param name="DelayDays">Days after the last pass, for a delay recurrence.</param>
public sealed record RecurrenceDto(string Kind, string Unit, int Every, IReadOnlyList<int> Days, int MonthDay, int DelayDays)
{
    public static RecurrenceDto From(Recurrence r) => new(r.Kind, r.Unit, r.Every, r.WeekDays, r.MonthDay, r.DelayDays);
}

/// <param name="State">active, paused or stopped.</param>
/// <param name="NextDueOn">The open due day; none while paused or stopped.</param>
/// <param name="Streak">Due days done on time in a row; a late or skipped one resets it.</param>
public sealed record TaskRoutineDto(Guid Id, Guid MemberProfileId, string Title, string? Notes, int? DurationMinutes, string Priority,
    IReadOnlyList<string> Tags, RecurrenceDto Recurrence, DateOnly StartOn, string State, DateOnly? NextDueOn, bool IsLate,
    int Streak, int Record, DateOnly? LastDoneOn, bool DoneToday, bool HasImage, DateTimeOffset UpdatedAt)
{
    public static TaskRoutineDto From(TaskRoutine r, DateOnly today)
    {
        var status = RoutineSchedule.Compute(r, today);
        return new(r.Id, r.MemberProfileId, r.Title, r.Notes, r.DurationMinutes, r.Priority, r.Tags, RecurrenceDto.From(r.Recurrence),
            r.StartOn, r.State, status.NextDueOn, status.IsLate, status.Streak, status.Record, status.LastDoneOn, status.DoneToday,
            r.HasImage, r.UpdatedAt);
    }
}

/// <param name="Kind">done or skipped.</param>
/// <param name="DueOn">The due day this pass closed.</param>
/// <param name="OnTime">Done at the latest on its due day.</param>
public sealed record RoutinePassDto(Guid Id, string Kind, DateOnly Day, DateOnly DueOn, bool OnTime, string? Note);

/// <summary>A routine with its passes, newest first.</summary>
public sealed record TaskRoutineDetailDto(TaskRoutineDto Routine, IReadOnlyList<RoutinePassDto> Passes)
{
    public static TaskRoutineDetailDto From(TaskRoutine r, DateOnly today) =>
        new(TaskRoutineDto.From(r, today), r.Passes.OrderByDescending(p => p.DueOn).ThenByDescending(p => p.Day)
            .Select(p => new RoutinePassDto(p.Id, p.Kind, p.Day, p.DueOn, p.OnTime, p.Note)).ToList());
}
