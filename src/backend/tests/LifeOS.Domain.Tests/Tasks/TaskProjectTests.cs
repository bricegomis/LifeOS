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
    public void A_project_moves_under_another_but_never_under_itself_or_its_sub_projects()
    {
        var household = Guid.NewGuid();
        var land = TaskProject.Create(household, "Terrain", null, null, Now);
        var chalet = TaskProject.Create(household, "Chalet", null, null, Now);
        var ceiling = TaskProject.Create(household, "Plafond", null, null, Now);
        var all = new[] { land, chalet, ceiling };
        Guid? ParentOf(Guid id) => all.Single(p => p.Id == id).ParentId;

        chalet.MoveUnder(land.Id, ParentOf, Now);
        ceiling.MoveUnder(chalet.Id, ParentOf, Now);
        Assert.Equal((land.Id, chalet.Id), (chalet.ParentId!.Value, ceiling.ParentId!.Value));
        Assert.Throws<ArgumentException>(() => land.MoveUnder(land.Id, ParentOf, Now));
        Assert.Throws<ArgumentException>(() => land.MoveUnder(ceiling.Id, ParentOf, Now));
        Assert.Null(land.ParentId);
        ceiling.MoveUnder(Guid.Empty, ParentOf, Now);
        Assert.Null(ceiling.ParentId);
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
