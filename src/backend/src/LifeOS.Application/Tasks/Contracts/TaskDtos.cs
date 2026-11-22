using LifeOS.Domain.Tasks;

namespace LifeOS.Application.Tasks.Contracts;

public sealed record ChecklistItemDto(Guid Id, string Label, bool Done);

/// <param name="State">open, done or abandoned.</param>
/// <param name="IsLate">Open with a due day before today (Paris).</param>
public sealed record TodoDto(Guid Id, Guid MemberProfileId, string Title, string? Notes, DateOnly? DueOn, int? DurationMinutes,
    string Priority, IReadOnlyList<string> Tags, string State, DateOnly? DoneOn, bool IsLate, bool HasImage,
    IReadOnlyList<ChecklistItemDto> Checklist, DateTimeOffset UpdatedAt)
{
    public static TodoDto From(Todo t, DateOnly today) =>
        new(t.Id, t.MemberProfileId, t.Title, t.Notes, t.DueOn, t.DurationMinutes, t.Priority, t.Tags, t.State, t.DoneOn,
            t.IsLate(today), t.HasImage, t.OrderedChecklist.Select(i => new ChecklistItemDto(i.Id, i.Label, i.Done)).ToList(),
            t.UpdatedAt);
}

/// <summary>
/// The "Aujourd'hui" page (ADR 0017): open todos due up to <paramref name="Today"/> + 7 days (late ones
/// included), open todos without a due day, and those done today.
/// </summary>
public sealed record TaskTodayDto(DateOnly Today, IReadOnlyList<TodoDto> Todos);
