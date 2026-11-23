using LifeOS.Application.Tasks.Contracts;
using LifeOS.Application.Tests.Food;
using LifeOS.Domain.Tasks;

namespace LifeOS.Application.Tests.Tasks;

public sealed class TaskReadContractTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    // 2026-11-22 23:30 UTC is already Monday 23 in Paris.
    private static readonly FixedClock LateEvening = new(new DateTimeOffset(2026, 11, 22, 23, 30, 0, TimeSpan.Zero));
    private static readonly DateOnly Monday = new(2026, 11, 23);
    private static readonly DateTimeOffset Now = LateEvening.GetUtcNow();

    private sealed class FakeData : ITaskReadData
    {
        public List<Todo> Todos { get; } = [];
        public List<TaskRoutine> Routines { get; } = [];

        public Task<IReadOnlyList<Todo>> OpenTodosAsync(Guid householdId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Todo>>(Todos.Where(t => t.HouseholdId == householdId && t.IsOpen).ToList());
        public Task<IReadOnlyList<Todo>> DoneTodosAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Todo>>(Todos.Where(t => t.HouseholdId == householdId && t.State == "done"
                && t.DoneOn >= from && t.DoneOn <= to).ToList());
        public Task<IReadOnlyList<TaskRoutine>> RoutinesAsync(Guid householdId, bool activeOnly, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TaskRoutine>>(Routines.Where(r => r.HouseholdId == householdId && (!activeOnly || r.IsActive)).ToList());
    }

    private static Todo Todo(string title, DateOnly? due, int? minutes = null, string? priority = null, Guid? profile = null) =>
        Domain.Tasks.Todo.Create(Household, profile ?? Profile, title, null, due, minutes, priority, null, Now);

    private static TaskRoutine Routine(string title, Recurrence recurrence, DateOnly start, int? minutes = null, Guid? profile = null) =>
        TaskRoutine.Create(Household, profile ?? Profile, title, null, minutes, null, null, recurrence, start, Now);

    [Fact]
    public void The_task_day_is_the_day_in_Paris() =>
        Assert.Equal(Monday, new TaskReadContract(new FakeData(), LateEvening).Today());

    [Fact]
    public async Task Open_todos_come_late_first_then_by_due_day_and_priority_without_due_day_last()
    {
        var data = new FakeData();
        var done = Todo("Fait", Monday);
        done.Complete(Monday, Monday, Now);
        var abandoned = Todo("Abandonné", Monday);
        abandoned.Abandon(Now);
        data.Todos.AddRange([Todo("Sans échéance", null), Todo("Demain normal", Monday.AddDays(1), 30),
            Todo("Demain haute", Monday.AddDays(1), priority: "high", profile: Other), Todo("En retard", Monday.AddDays(-2)), done, abandoned,
            Domain.Tasks.Todo.Create(Guid.NewGuid(), Profile, "Autre foyer", null, Monday, null, null, null, Now)]);

        var todos = await new TaskReadContract(data, LateEvening).OpenTodosAsync(Household, CancellationToken.None);

        Assert.Equal(["En retard", "Demain haute", "Demain normal", "Sans échéance"], todos.Select(t => t.Title));
        Assert.Equal((true, false), (todos[0].IsLate, todos[1].IsLate));
        Assert.Equal((Other, "high"), (todos[1].MemberProfileId, todos[1].Priority));
        Assert.Equal(30, todos[2].DurationMinutes);
    }

    [Fact]
    public async Task Routine_dues_mark_the_open_day_and_project_the_next_ones()
    {
        var data = new FakeData();
        var bins = Routine("Poubelles", Recurrence.Weekly(1, [1, 4]), Monday, 5);
        var sheets = Routine("Draps", Recurrence.Delay(7), Monday.AddDays(-3));
        var paused = Routine("En pause", Recurrence.FixedDays(1), Monday);
        paused.Pause(Now);
        data.Routines.AddRange([bins, sheets, paused]);

        var dues = await new TaskReadContract(data, LateEvening).RoutineDuesAsync(Household, Monday.AddDays(7), CancellationToken.None);

        Assert.Equal([("Draps", Monday.AddDays(-3), true, true), ("Poubelles", Monday, true, false), ("Poubelles", Monday.AddDays(3), false, false),
            ("Draps", Monday.AddDays(7), false, false), ("Poubelles", Monday.AddDays(7), false, false)],
            dues.Select(d => (d.Title, d.DueOn, d.IsOpen, d.IsLate)));
        Assert.Equal(5, dues[1].DurationMinutes);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new TaskReadContract(data, LateEvening).RoutineDuesAsync(Household, Monday.AddDays(-1), CancellationToken.None));
    }

    [Fact]
    public async Task Days_gather_done_todos_and_done_passes_per_profile()
    {
        var data = new FakeData();
        var todo = Todo("Appeler le plombier", Monday.AddDays(-1), 15);
        todo.Complete(Monday.AddDays(-1), Monday, Now);
        var other = Todo("Réserver", null, profile: Other);
        other.Complete(Monday.AddDays(-1), Monday, Now);
        var bins = Routine("Poubelles", Recurrence.FixedDays(1), Monday.AddDays(-2), 5);
        bins.Complete(Monday.AddDays(-2), null, Monday, Now);
        bins.Complete(Monday.AddDays(-1), null, Monday, Now);
        bins.Skip(Monday, Now);
        bins.Stop(Now);
        data.Todos.AddRange([todo, other]);
        data.Routines.Add(bins);

        var days = await new TaskReadContract(data, LateEvening).DaysAsync(Household, Monday.AddDays(-1), Monday, CancellationToken.None);

        Assert.Equal(2, days.Count);
        var mine = Assert.Single(days, d => d.MemberProfileId == Profile);
        Assert.Equal((Monday.AddDays(-1), 20, true), (mine.Date, mine.DurationMinutes, mine.DurationComplete));
        Assert.Equal(["todo", "routine"], mine.Done.Select(d => d.Kind));
        var theirs = Assert.Single(days, d => d.MemberProfileId == Other);
        Assert.Equal((0, false), (theirs.DurationMinutes, theirs.DurationComplete));
    }
}
