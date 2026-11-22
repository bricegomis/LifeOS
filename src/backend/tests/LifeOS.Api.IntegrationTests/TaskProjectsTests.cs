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
        // A todo cannot join another household's project.
        Assert.Equal(HttpStatusCode.BadRequest, (await Todo(other, "Ailleurs", project.Id)).StatusCode);
    }
}
