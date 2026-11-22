using LifeOS.Domain.Tasks;

namespace LifeOS.Domain.Tests.Tasks;

public sealed class TaskProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_project_is_finished_or_abandoned_once_and_can_be_reopened()
    {
        var project = TaskProject.Create(Guid.NewGuid(), "  Chambre de Léa ", " ", new DateOnly(2026, 12, 20), Now);
        Assert.Equal(("Chambre de Léa", null, "active"), (project.Title, project.Notes, project.State));
        project.Finish(Now);
        Assert.Equal("done", project.State);
        Assert.Throws<ArgumentException>(() => project.Abandon(Now));
        project.Reopen(Now);
        project.Abandon(Now);
        Assert.Equal("abandoned", project.State);
        Assert.Throws<ArgumentException>(() => TaskProject.Create(Guid.NewGuid(), " ", null, null, Now));
    }

    [Fact]
    public void A_todo_moves_into_and_out_of_a_project()
    {
        var todo = Todo.Create(Guid.NewGuid(), Guid.NewGuid(), "Peindre", null, null, null, null, null, Now);
        var project = Guid.NewGuid();
        todo.MoveToProject(project, Now);
        Assert.Equal(project, todo.ProjectId);
        todo.MoveToProject(null, Now);
        Assert.Null(todo.ProjectId);
    }
}
