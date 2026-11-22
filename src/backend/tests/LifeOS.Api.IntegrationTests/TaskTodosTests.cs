using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeOS.Api.Tasks;
using LifeOS.Application.Tasks.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0017: todos with their checklist, image, today page and search, per household.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TaskTodosTests(PostgresContainerFixture postgres)
{
    private static async Task<TodoDto> Read(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TodoDto>())!;
    }

    private static Task<HttpResponseMessage> Create(HttpClient client, string title, DateOnly? due, params string[] tags) =>
        client.PostAsJsonAsync("/api/tasks/todos", new TodoRequest(null, title, null, due, 15, null, tags));

    [Fact]
    public async Task A_todo_is_created_on_the_main_profile_done_reopened_and_abandoned()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var created = await Create(client, "Renouveler l'assurance auto", new DateOnly(2020, 1, 1), "voiture", "Voiture");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var todo = await Read(created);
        Assert.NotEqual(Guid.Empty, todo.MemberProfileId);
        Assert.Equal(("open", true, "normal"), (todo.State, todo.IsLate, todo.Priority));
        Assert.Equal(["voiture"], todo.Tags);

        todo = await Read(await client.PostAsJsonAsync($"/api/tasks/todos/{todo.Id}/complete", new TodoCompleteRequest(null)));
        Assert.Equal("done", todo.State);
        Assert.NotNull(todo.DoneOn);
        Assert.False(todo.IsLate);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/tasks/todos/{todo.Id}/abandon", null)).StatusCode);

        todo = await Read(await client.PostAsync($"/api/tasks/todos/{todo.Id}/reopen", null));
        Assert.Equal("open", todo.State);
        todo = await Read(await client.PostAsync($"/api/tasks/todos/{todo.Id}/abandon", null));
        Assert.Equal("abandoned", todo.State);

        // Postponing is an update with a new due day.
        todo = await Read(await client.PostAsync($"/api/tasks/todos/{todo.Id}/reopen", null));
        todo = await Read(await client.PutAsJsonAsync($"/api/tasks/todos/{todo.Id}",
            new TodoRequest(null, todo.Title, "Comparer trois devis.", new DateOnly(2099, 1, 1), 30, "high", ["administratif"])));
        Assert.Equal((new DateOnly(2099, 1, 1), false, "high", "Comparer trois devis."), (todo.DueOn, todo.IsLate, todo.Priority, todo.Notes));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tasks/todos",
            new TodoRequest(null, "X", null, null, null, "urgent", null))).StatusCode);
    }

    [Fact]
    public async Task The_checklist_is_kept_in_order_and_does_not_complete_the_todo()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var todo = await Read(await Create(client, "Acheter la peinture", null));
        todo = await Read(await client.PostAsJsonAsync($"/api/tasks/todos/{todo.Id}/checklist", new ChecklistItemRequest("Blanc cassé")));
        todo = await Read(await client.PostAsJsonAsync($"/api/tasks/todos/{todo.Id}/checklist", new ChecklistItemRequest("Rouleau")));
        var first = todo.Checklist[0].Id;
        todo = await Read(await client.PutAsJsonAsync($"/api/tasks/todos/{todo.Id}/checklist/{first}", new ChecklistItemRequest("Blanc cassé 2,5 L", true)));
        Assert.Equal([("Blanc cassé 2,5 L", true), ("Rouleau", false)], todo.Checklist.Select(i => (i.Label, i.Done)));
        todo = await Read(await client.DeleteAsync($"/api/tasks/todos/{todo.Id}/checklist/{todo.Checklist[1].Id}"));
        todo = await Read(await client.GetAsync($"/api/tasks/todos/{todo.Id}"));
        Assert.Single(todo.Checklist);
        Assert.Equal("open", todo.State);
    }

    [Fact]
    public async Task Today_lists_late_soon_and_undated_open_todos_and_search_ignores_accents()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var today = (await client.GetFromJsonAsync<TaskTodayDto>("/api/tasks/today"))!.Today;
        await Create(client, "En retard", today.AddDays(-3), "maison");
        await Create(client, "Bientôt", today.AddDays(7));
        await Create(client, "Plus tard", today.AddDays(8));
        await Create(client, "Sans échéance", null, "Maison");
        var done = await Read(await Create(client, "Déjà fait", today));
        await client.PostAsJsonAsync($"/api/tasks/todos/{done.Id}/complete", new TodoCompleteRequest(null));

        var page = (await client.GetFromJsonAsync<TaskTodayDto>("/api/tasks/today"))!;
        Assert.Equal(["En retard", "Bientôt", "Sans échéance", "Déjà fait"], page.Todos.Select(t => t.Title));

        var found = (await client.GetFromJsonAsync<List<TodoDto>>("/api/tasks/todos?q=ECHEANCE%20sans"))!;
        Assert.Equal("Sans échéance", Assert.Single(found).Title);
        var tagged = (await client.GetFromJsonAsync<List<TodoDto>>("/api/tasks/todos?tag=maison"))!;
        Assert.Equal(2, tagged.Count);
        var doneOnly = (await client.GetFromJsonAsync<List<TodoDto>>("/api/tasks/todos?state=done"))!;
        Assert.Equal("Déjà fait", Assert.Single(doneOnly).Title);
        Assert.Equal(5, (await client.GetFromJsonAsync<List<TodoDto>>("/api/tasks/todos?state=all"))!.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tasks/todos?state=late")).StatusCode);
        Assert.Equal(["maison"], (await client.GetFromJsonAsync<List<string>>("/api/tasks/tags"))!);
    }

    [Fact]
    public async Task Todos_and_their_images_stay_in_their_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var todo = await Read(await Create(client, "Monter l'étagère", null, "maison"));

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "etagere.png");
        Assert.True((await Read(await client.PutAsync($"/api/tasks/todos/{todo.Id}/image", form))).HasImage);
        using (var read = await client.GetAsync($"/api/tasks/todos/{todo.Id}/image"))
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tasks/todos/{todo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tasks/todos/{todo.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/tasks/todos/{todo.Id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/tasks/todos/{todo.Id}",
            new TodoRequest(null, "Piratage", null, null, null, null, null))).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<TodoDto>>("/api/tasks/todos?state=all"))!);
        Assert.Empty((await other.GetFromJsonAsync<TaskTodayDto>("/api/tasks/today"))!.Todos);
        Assert.Empty((await other.GetFromJsonAsync<List<string>>("/api/tasks/tags"))!);

        // A profile of another household is rejected.
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/tasks/todos",
            new TodoRequest(todo.MemberProfileId, "Ailleurs", null, null, null, null, null))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tasks/todos/{todo.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tasks/todos/{todo.Id}/image")).StatusCode);
    }
}
