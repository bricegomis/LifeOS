using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Tasks;
using LifeOS.Application.Tasks.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0017: projects group todos, with a computed progress, per household.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TaskProjectsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<HttpResponseMessage> Todo(HttpClient client, string title, Guid? project) =>
        client.PostAsJsonAsync("/api/tasks/todos", new TodoRequest(null, title, null, null, null, null, null, project));

    [Fact]
    public async Task A_project_counts_its_todos_and_finishing_it_leaves_them_open()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var created = await client.PostAsJsonAsync("/api/tasks/projects", new TaskProjectRequest("Chambre de Léa", "Avant Noël.", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = (await Read<TaskProjectDetailDto>(created)).Project;

        var paint = await Read<TodoDto>(await Todo(client, "Acheter la peinture", project.Id));
        Assert.Equal((project.Id, "Chambre de Léa"), (paint.ProjectId!.Value, paint.ProjectTitle));
        var shelf = await Read<TodoDto>(await Todo(client, "Monter l'étagère", project.Id));
        var sort = await Read<TodoDto>(await Todo(client, "Trier les vêtements", project.Id));
        await client.PostAsJsonAsync($"/api/tasks/todos/{paint.Id}/complete", new TodoCompleteRequest(null));
        await client.PostAsync($"/api/tasks/todos/{sort.Id}/abandon", null);

        var detail = await Read<TaskProjectDetailDto>(await client.GetAsync($"/api/tasks/projects/{project.Id}"));
        Assert.Equal((1, 2, 1), (detail.Project.Done, detail.Project.Total, detail.Project.Open));
        Assert.Equal(3, detail.Todos.Count);
        Assert.Single((await client.GetFromJsonAsync<List<TodoDto>>($"/api/tasks/todos?projectId={project.Id}"))!);
        Assert.Single((await client.GetFromJsonAsync<List<TodoDto>>("/api/tasks/todos?q=lea"))!);

        detail = await Read<TaskProjectDetailDto>(await client.PostAsync($"/api/tasks/projects/{project.Id}/finish", null));
        Assert.Equal("done", detail.Project.State);
        Assert.Equal("open", (await client.GetFromJsonAsync<TodoDto>($"/api/tasks/todos/{shelf.Id}"))!.State);
        Assert.Empty((await client.GetFromJsonAsync<List<TaskProjectDto>>("/api/tasks/projects"))!);
        Assert.Single((await client.GetFromJsonAsync<List<TaskProjectDto>>("/api/tasks/projects?state=closed"))!);

        // A closed project takes no new todo, but its todos keep it when edited; a todo leaves it with no project.
        Assert.Equal(HttpStatusCode.BadRequest, (await Todo(client, "Encore une", project.Id)).StatusCode);
        var kept = await Read<TodoDto>(await client.PutAsJsonAsync($"/api/tasks/todos/{shelf.Id}",
            new TodoRequest(null, "Monter l'étagère du haut", null, null, null, null, null, project.Id)));
        Assert.Equal(project.Id, kept.ProjectId);
        var loose = await Read<TodoDto>(await client.PutAsJsonAsync($"/api/tasks/todos/{shelf.Id}",
            new TodoRequest(null, "Monter l'étagère du haut", null, null, null, null, null, null)));
        Assert.Null(loose.ProjectId);
    }

    [Fact]
    public async Task Sub_projects_form_a_tree_and_their_todos_count_in_their_ancestors()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        async Task<TaskProjectDto> Create(string title, Guid? parent) => (await Read<TaskProjectDetailDto>(
            await client.PostAsJsonAsync("/api/tasks/projects", new TaskProjectRequest(title, null, null, parent)))).Project;
        var land = await Create("Terrain", null);
        var chalet = await Create("Chalet", land.Id);
        var garden = await Create("Potager", land.Id);
        var ceiling = await Create("Plafond", chalet.Id);
        Assert.Equal(land.Id, chalet.ParentId);

        var plank = await Read<TodoDto>(await Todo(client, "Acheter les lambris", ceiling.Id));
        await Read<TodoDto>(await Todo(client, "Bêcher", garden.Id));
        await Read<TodoDto>(await Todo(client, "Borner le terrain", land.Id));
        await client.PostAsJsonAsync($"/api/tasks/todos/{plank.Id}/complete", new TodoCompleteRequest(null));

        var detail = await Read<TaskProjectDetailDto>(await client.GetAsync($"/api/tasks/projects/{land.Id}"));
        Assert.Equal((1, 3, 2), (detail.Project.Done, detail.Project.Total, detail.Project.SubProjects));
        Assert.Single(detail.Todos);
        Assert.Equal(["Chalet", "Potager"], detail.SubProjects.Select(p => p.Title));
        Assert.Equal((1, 1), (detail.SubProjects[0].Done, detail.SubProjects[0].Total));
        Assert.Empty(detail.Ancestors);
        detail = await Read<TaskProjectDetailDto>(await client.GetAsync($"/api/tasks/projects/{ceiling.Id}"));
        Assert.Equal(["Terrain", "Chalet"], detail.Ancestors.Select(a => a.Title));
        Assert.Equal(4, (await client.GetFromJsonAsync<List<TaskProjectDto>>("/api/tasks/projects"))!.Count);

        // No cycle, no closed parent; finishing a parent leaves its sub-projects in progress.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/tasks/projects/{land.Id}",
            new TaskProjectRequest("Terrain", null, null, ceiling.Id))).StatusCode);
        await Read<TaskProjectDetailDto>(await client.PostAsync($"/api/tasks/projects/{chalet.Id}/finish", null));
        Assert.Equal("active", (await client.GetFromJsonAsync<TaskProjectDetailDto>($"/api/tasks/projects/{ceiling.Id}"))!.Project.State);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tasks/projects",
            new TaskProjectRequest("Elec", null, null, chalet.Id))).StatusCode);
        // A sub-project keeps its closed parent when edited, and moves to the top without one.
        var kept = await Read<TaskProjectDetailDto>(await client.PutAsJsonAsync($"/api/tasks/projects/{ceiling.Id}",
            new TaskProjectRequest("Plafond du chalet", null, null, chalet.Id)));
        Assert.Equal(chalet.Id, kept.Project.ParentId);
        var top = await Read<TaskProjectDetailDto>(await client.PutAsJsonAsync($"/api/tasks/projects/{ceiling.Id}",
            new TaskProjectRequest("Plafond du chalet", null, null, null)));
        Assert.Null(top.Project.ParentId);
    }

    [Fact]
    public async Task Projects_stay_in_their_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var project = (await Read<TaskProjectDetailDto>(await client.PostAsJsonAsync("/api/tasks/projects",
            new TaskProjectRequest("Dossier impôts", null, null)))).Project;

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tasks/projects/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/tasks/projects/{project.Id}/finish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tasks/projects/{project.Id}/image")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<TaskProjectDto>>("/api/tasks/projects?state=all"))!);
        // Neither a todo nor a sub-project can join another household's project.
        Assert.Equal(HttpStatusCode.BadRequest, (await Todo(other, "Ailleurs", project.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/tasks/projects",
            new TaskProjectRequest("Sous-dossier", null, null, project.Id))).StatusCode);
    }
}
