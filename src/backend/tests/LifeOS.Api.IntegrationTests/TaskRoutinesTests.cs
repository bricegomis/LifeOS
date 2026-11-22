using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeOS.Api.Tasks;
using LifeOS.Application.Tasks.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0017: household routines, their passes and streak, on the today page and per household.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TaskRoutinesTests(PostgresContainerFixture postgres)
{
    private static async Task<TaskRoutineDetailDto> Read(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TaskRoutineDetailDto>())!;
    }

    private static Task<HttpResponseMessage> Create(HttpClient client, string title, RecurrenceRequest recurrence, DateOnly? start = null) =>
        client.PostAsJsonAsync("/api/tasks/routines", new TaskRoutineRequest(null, title, null, 10, null, ["maison"], recurrence, start));

    private static async Task<DateOnly> Today(HttpClient client) => (await client.GetFromJsonAsync<TaskTodayDto>("/api/tasks/today"))!.Today;

    [Fact]
    public async Task A_delay_routine_is_done_skipped_and_its_pass_removed()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var today = await Today(client);
        var created = await Create(client, "Changer les draps", new RecurrenceRequest("delay", null, null, null, null, 7), today.AddDays(-3));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var routine = await Read(created);
        Assert.Equal((today.AddDays(-3), true, "active"), (routine.Routine.NextDueOn, routine.Routine.IsLate, routine.Routine.State));
        Assert.Contains(routine.Routine.Id, (await client.GetFromJsonAsync<TaskTodayDto>("/api/tasks/today"))!.Routines.Select(r => r.Id));

        // Done yesterday, late: the streak stays at zero; the next due day counts from the pass.
        routine = await Read(await client.PostAsJsonAsync($"/api/tasks/routines/{routine.Routine.Id}/done", new RoutineDoneRequest(today.AddDays(-1), "Draps d'hiver")));
        Assert.Equal((today.AddDays(6), 0, today.AddDays(-1)), (routine.Routine.NextDueOn, routine.Routine.Streak, routine.Routine.LastDoneOn));
        var pass = Assert.Single(routine.Passes);
        Assert.Equal(("done", false, "Draps d'hiver"), (pass.Kind, pass.OnTime, pass.Note));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/tasks/routines/{routine.Routine.Id}/done",
            new RoutineDoneRequest(today.AddDays(1), null))).StatusCode);

        routine = await Read(await client.PostAsync($"/api/tasks/routines/{routine.Routine.Id}/skip", null));
        Assert.Equal(("skipped", today.AddDays(7)), (routine.Passes[0].Kind, routine.Routine.NextDueOn!.Value));
        routine = await Read(await client.DeleteAsync($"/api/tasks/routines/{routine.Routine.Id}/passes/{routine.Passes[0].Id}"));
        Assert.Equal(today.AddDays(6), routine.Routine.NextDueOn);
    }

    [Fact]
    public async Task A_weekly_routine_pauses_resumes_and_stops()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var routine = await Read(await Create(client, "Sortir les poubelles", new RecurrenceRequest("fixed", "week", 1, [1, 4], null, null)));
        Assert.Equal([1, 4], routine.Routine.Recurrence.Days);
        Assert.Contains(routine.Routine.NextDueOn!.Value.DayOfWeek, new[] { DayOfWeek.Monday, DayOfWeek.Thursday });

        routine = await Read(await client.PostAsync($"/api/tasks/routines/{routine.Routine.Id}/pause", null));
        Assert.Equal(("paused", (DateOnly?)null), (routine.Routine.State, routine.Routine.NextDueOn));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/tasks/routines/{routine.Routine.Id}/skip", null)).StatusCode);
        routine = await Read(await client.PostAsync($"/api/tasks/routines/{routine.Routine.Id}/resume", null));
        Assert.False(routine.Routine.IsLate);

        routine = await Read(await client.PutAsJsonAsync($"/api/tasks/routines/{routine.Routine.Id}", new TaskRoutineRequest(null, "Sortir les poubelles",
            "Jaune le lundi.", 5, "high", ["maison"], new RecurrenceRequest("fixed", "month", 1, null, 31, null), null)));
        Assert.Equal(("month", 31, "high"), (routine.Routine.Recurrence.Unit, routine.Routine.Recurrence.MonthDay, routine.Routine.Priority));

        routine = await Read(await client.PostAsync($"/api/tasks/routines/{routine.Routine.Id}/stop", null));
        Assert.Equal("stopped", routine.Routine.State);
        Assert.Empty((await client.GetFromJsonAsync<List<TaskRoutineDto>>("/api/tasks/routines"))!);
        Assert.Single((await client.GetFromJsonAsync<List<TaskRoutineDto>>("/api/tasks/routines?state=stopped"))!);
        Assert.Single((await client.GetFromJsonAsync<List<TaskRoutineDto>>("/api/tasks/routines?state=all&q=POUBELLE"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, "Sans jour", new RecurrenceRequest("fixed", "week", 1, [], null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, "Inconnue", new RecurrenceRequest("monthly", null, null, null, null, null))).StatusCode);
    }

    [Fact]
    public async Task Routines_and_their_images_stay_in_their_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var routine = await Read(await Create(client, "Arroser les plantes", new RecurrenceRequest("delay", null, null, null, null, 4)));

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "plante.png");
        Assert.True((await Read(await client.PutAsync($"/api/tasks/routines/{routine.Routine.Id}/image", form))).Routine.HasImage);

        var id = routine.Routine.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tasks/routines/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tasks/routines/{id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/tasks/routines/{id}/done", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/tasks/routines/{id}/stop", null)).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<TaskRoutineDto>>("/api/tasks/routines?state=all"))!);
        Assert.Empty((await other.GetFromJsonAsync<TaskTodayDto>("/api/tasks/today"))!.Routines);
        Assert.Empty((await other.GetFromJsonAsync<List<string>>("/api/tasks/tags"))!);
        Assert.Equal(["maison"], (await client.GetFromJsonAsync<List<string>>("/api/tasks/tags"))!);
    }
}
