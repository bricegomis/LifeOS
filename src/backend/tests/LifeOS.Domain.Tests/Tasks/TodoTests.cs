using LifeOS.Domain.Tasks;

namespace LifeOS.Domain.Tests.Tasks;

public sealed class TodoTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static Todo Create(DateOnly? due = null, IEnumerable<string>? tags = null, string? priority = null) =>
        Todo.Create(Household, Profile, "  Renouveler l'assurance  ", "  ", due, 30, priority, tags, Now);

    [Fact]
    public void A_new_todo_is_open_normal_priority_with_trimmed_text()
    {
        var todo = Create();
        Assert.Equal(("Renouveler l'assurance", null, "normal", "open"), (todo.Title, todo.Notes, todo.Priority, todo.State));
        Assert.Null(todo.DoneOn);
        Assert.False(todo.HasImage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_title_is_required(string title) =>
        Assert.Throws<ArgumentException>(() => Todo.Create(Household, Profile, title, null, null, null, null, null, Now));

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void The_duration_stays_between_one_minute_and_one_day(int minutes) =>
        Assert.Throws<ArgumentException>(() => Todo.Create(Household, Profile, "A", null, null, minutes, null, null, Now));

    [Fact]
    public void An_unknown_priority_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Create(priority: "urgent"));

    [Fact]
    public void Tags_are_trimmed_and_deduplicated_ignoring_case()
    {
        var todo = Create(tags: [" maison ", "Maison", "", "voiture"]);
        Assert.Equal(["maison", "voiture"], todo.Tags);
        Assert.Throws<ArgumentException>(() => Create(tags: Enumerable.Range(0, 21).Select(i => $"t{i}")));
    }

    [Fact]
    public void An_open_todo_is_late_only_after_its_due_day()
    {
        Assert.True(Create(Today.AddDays(-1)).IsLate(Today));
        Assert.False(Create(Today).IsLate(Today));
        Assert.False(Create().IsLate(Today));
        var done = Create(Today.AddDays(-3));
        done.Complete(Today, Today, Now);
        Assert.False(done.IsLate(Today));
    }

    [Fact]
    public void Done_keeps_its_day_and_can_be_reopened()
    {
        var todo = Create(Today.AddDays(-2));
        todo.Complete(Today.AddDays(-1), Today, Now);
        Assert.Equal(("done", Today.AddDays(-1)), (todo.State, todo.DoneOn));
        Assert.Throws<ArgumentException>(() => todo.Complete(Today, Today, Now));
        Assert.Throws<ArgumentException>(() => todo.Abandon(Now));
        todo.Reopen(Now);
        Assert.Equal(("open", (DateOnly?)null, (DateOnly?)Today.AddDays(-2)), (todo.State, todo.DoneOn, todo.DueOn));
        Assert.Throws<ArgumentException>(() => todo.Reopen(Now));
    }

    [Fact]
    public void A_todo_cannot_be_done_in_the_future() =>
        Assert.Throws<ArgumentException>(() => Create().Complete(Today.AddDays(1), Today, Now));

    [Fact]
    public void An_abandoned_todo_can_be_reopened()
    {
        var todo = Create();
        todo.Abandon(Now);
        Assert.Equal("abandoned", todo.State);
        todo.Reopen(Now);
        Assert.True(todo.IsOpen);
    }

    [Fact]
    public void The_checklist_keeps_its_order_and_ticking_everything_does_not_complete_the_todo()
    {
        var todo = Create();
        var paint = todo.AddChecklistItem(" Blanc cassé ", Now);
        var roller = todo.AddChecklistItem("Rouleau", Now);
        todo.AddChecklistItem("Bâche", Now);
        todo.RemoveChecklistItem(roller.Id, Now);
        var tarp = todo.AddChecklistItem("Ruban", Now);
        Assert.Equal(["Blanc cassé", "Bâche", "Ruban"], todo.OrderedChecklist.Select(i => i.Label));
        foreach (var item in todo.Checklist.ToList()) todo.UpdateChecklistItem(item.Id, item.Label, true, Now);
        Assert.True(todo.Checklist.All(i => i.Done));
        Assert.True(todo.IsOpen);
        Assert.Throws<ArgumentException>(() => todo.UpdateChecklistItem(Guid.NewGuid(), "x", true, Now));
        Assert.Throws<ArgumentException>(() => todo.UpdateChecklistItem(paint.Id, " ", true, Now));
        Assert.NotEqual(paint.Id, tarp.Id);
    }

    [Fact]
    public void A_checklist_has_at_most_fifty_items()
    {
        var todo = Create();
        for (var i = 0; i < Todo.MaxChecklistItems; i++) todo.AddChecklistItem($"Item {i}", Now);
        Assert.Throws<ArgumentException>(() => todo.AddChecklistItem("Un de trop", Now));
    }
}
