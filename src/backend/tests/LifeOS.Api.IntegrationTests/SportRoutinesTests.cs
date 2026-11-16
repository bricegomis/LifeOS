using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeOS.Api.Sports;
using LifeOS.Application.Sports.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0014: the exercise library, daily routines and their ticks, per household and profile.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SportRoutinesTests(PostgresContainerFixture postgres)
{
    private static async Task<ExerciseDto> Exercise(HttpClient client, string name, string measure, bool withLoad) =>
        (await (await client.PostAsJsonAsync("/api/sport/exercises", new ExerciseRequest(name, "Consigne.", measure, withLoad)))
            .Content.ReadFromJsonAsync<ExerciseDto>())!;

    private static async Task<RoutineReportDto> Report(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RoutineReportDto>())!;
    }

    [Fact]
    public async Task A_routine_is_done_when_every_exercise_is_ticked_and_keeps_what_was_really_done()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var wallSit = await Exercise(client, "Chaise contre le mur", "hold", true);
        var bridge = await Exercise(client, "Pont fessier", "reps", false);

        var created = await client.PostAsJsonAsync("/api/sport/routines", new RoutineRequest(null, "Tendons", null, 1,
            [new RoutineLineRequest(wallSit.Id, 2, 45, 10), new RoutineLineRequest(bridge.Id, 2, 12, null)]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var report = await Report(created);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], report.Routine.Days);
        Assert.Equal(["Chaise contre le mur", "Pont fessier"], report.Routine.Lines.Select(line => line.ExerciseName));
        Assert.True(report.PlannedToday);
        Assert.Equal((0, 2), (report.DoneToday, report.TotalToday));

        // Ticking without values records the target; ticking again corrects the day.
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{report.Routine.Id}/completions",
            new ExerciseCompletionRequest(wallSit.Id, null, null, null)));
        Assert.Equal((45, 10m), (report.TodayCompletions[0].Value, report.TodayCompletions[0].LoadKg));
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{report.Routine.Id}/completions",
            new ExerciseCompletionRequest(wallSit.Id, 60, 12.5m, null)));
        Assert.Equal((60, 12.5m), (Assert.Single(report.TodayCompletions).Value, report.TodayCompletions[0].LoadKg));
        Assert.False(report.CompletedToday);
        Assert.Equal(0, report.Streak);

        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{report.Routine.Id}/completions",
            new ExerciseCompletionRequest(bridge.Id, 8, null, null)));
        Assert.True(report.CompletedToday);
        Assert.Equal(1, report.Streak);
        Assert.Equal("done", report.Calendar[^1].Status);

        var progress = (await client.GetFromJsonAsync<ExerciseProgressDto>($"/api/sport/exercises/{wallSit.Id}/progress?weeks=2"))!;
        Assert.Equal((60, 12.5m, 1), (progress.BestValue, progress.BestLoadKg, progress.DaysDone));
        Assert.Equal(60, progress.Weeks[^1].BestValue);

        // A done exercise keeps its measure and cannot be archived while a routine uses it.
        var listed = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/sport/exercises"))!;
        Assert.True(listed.Single(e => e.Id == wallSit.Id).MeasureLocked);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sport/exercises/{wallSit.Id}",
            new ExerciseRequest("Chaise", null, "reps", true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/sport/exercises/{wallSit.Id}")).StatusCode);

        report = await Report(await client.DeleteAsync($"/api/sport/routines/{report.Routine.Id}/completions/{bridge.Id}"));
        Assert.Equal((1, 2, 0), (report.DoneToday, report.TotalToday, report.Streak));

        // The routine changes from today: same day, the version is edited in place.
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{report.Routine.Id}", new RoutineRequest(null, "Tendons du matin",
            [1, 2, 3, 4, 5, 6, 7], 0, [new RoutineLineRequest(wallSit.Id, 3, 50, 12)])));
        Assert.Equal("Tendons du matin", report.Routine.Name);
        Assert.Equal((3, 50, 12m), (report.Routine.Lines[0].Sets, report.Routine.Lines[0].Target, report.Routine.Lines[0].LoadKg));
        Assert.True(report.CompletedToday);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/exercises/{bridge.Id}")).StatusCode);

        var today = report.Today;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sport/routines/{report.Routine.Id}/completions",
            new ExerciseCompletionRequest(wallSit.Id, null, null, today.AddDays(2)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sport/routines",
            new RoutineRequest(null, "Archivé", null, 0, [new RoutineLineRequest(bridge.Id, 1, 10, null)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sport/routines",
            new RoutineRequest(null, "Jour inconnu", [8], 0, [new RoutineLineRequest(wallSit.Id, 1, 10, null)]))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/routines/{report.Routine.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines"))!);
        Assert.Single((await client.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines?includeStopped=true&calendarWeeks=12"))!);
    }

    [Fact]
    public async Task An_exercise_image_is_stored_and_served_only_to_its_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var exercise = await Exercise(client, "Chaise contre le mur", "hold", true);
        Assert.False(exercise.HasImage);

        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 1];
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "chaise.png");
        var upload = await client.PutAsync($"/api/sport/exercises/{exercise.Id}/image", form);
        Assert.True((await upload.Content.ReadFromJsonAsync<ExerciseDto>())!.HasImage);

        using var read = await client.GetAsync($"/api/sport/exercises/{exercise.Id}/image");
        Assert.Equal(png, await read.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/sport/exercises/{exercise.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/exercises/{exercise.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/sport/exercises/{exercise.Id}/image")).StatusCode);
    }

    [Fact]
    public async Task Another_household_sees_and_changes_none_of_it_and_cannot_point_to_it()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var exercise = await Exercise(client, "Gainage", "hold", false);
        var lines = new[] { new RoutineLineRequest(exercise.Id, 2, 30, null) };
        var routine = (await Report(await client.PostAsJsonAsync("/api/sport/routines", new RoutineRequest(null, "Dos", null, 0, lines)))).Routine;

        Assert.Empty((await other.GetFromJsonAsync<List<ExerciseDto>>("/api/sport/exercises?includeArchived=true"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines?includeStopped=true"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/exercises/{exercise.Id}",
            new ExerciseRequest("Volé", null, "hold", false))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/exercises/{exercise.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/sport/exercises/{exercise.Id}/progress")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/routines/{routine.Id}", new RoutineRequest(null, "Volée", null, 0, lines))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/routines/{routine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/routines/{routine.Id}/completions",
            new ExerciseCompletionRequest(exercise.Id, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/routines/{routine.Id}/completions/{exercise.Id}")).StatusCode);

        // Another household's exercise or profile is never accepted.
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/sport/routines", new RoutineRequest(null, "Dos", null, 0, lines))).StatusCode);
        var theirs = await Exercise(other, "Superman", "hold", false);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/sport/routines",
            new RoutineRequest(routine.MemberProfileId, "Dos", null, 0, [new RoutineLineRequest(theirs.Id, 1, 30, null)]))).StatusCode);

        Assert.Single((await client.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines"))!);
    }
}
