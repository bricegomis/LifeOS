using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeOS.Api.Sports;
using LifeOS.Application.Sports.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0014, ADR 0015: the exercise library, daily routines, their passes and ticks, per household and profile.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SportRoutinesTests(PostgresContainerFixture postgres)
{
    private static async Task<ExerciseDto> Exercise(HttpClient client, string name, string measure, decimal? defaultLoadKg) =>
        (await (await client.PostAsJsonAsync("/api/sport/exercises", new ExerciseRequest(name, "Consigne.", measure, defaultLoadKg)))
            .Content.ReadFromJsonAsync<ExerciseDto>())!;

    private static async Task<RoutineReportDto> Report(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RoutineReportDto>())!;
    }

    [Fact]
    public async Task A_routine_is_done_once_a_pass_has_every_set_and_later_passes_are_counted()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var wallSit = await Exercise(client, "Chaise contre le mur", "hold", 10);
        var bridge = await Exercise(client, "Pont fessier", "reps", null);
        Assert.Equal(10m, wallSit.DefaultLoadKg);

        // A line without a load takes the exercise's usual load.
        var created = await client.PostAsJsonAsync("/api/sport/routines", new RoutineRequest(null, "Tendons", null, 1,
            [new RoutineLineRequest(wallSit.Id, 2, 45, null), new RoutineLineRequest(bridge.Id, 2, 12, null)]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var report = await Report(created);
        var id = report.Routine.Id;
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], report.Routine.Days);
        Assert.Equal(new decimal?[] { 10, null }, report.Routine.Lines.Select(line => line.LoadKg));
        Assert.All(report.Routine.Lines, line => Assert.False(line.PerSide));
        Assert.Equal((0, 2, 0), (report.DoneToday, report.TotalToday, report.PassesToday));

        // One pass at a time; its sets are ticked one by one with the target.
        report = await Report(await client.PostAsJsonAsync($"/api/sport/routines/{id}/sessions", new RoutineSessionStartRequest(null)));
        var pass = Assert.Single(report.TodaySessions).Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/sport/routines/{id}/sessions", new RoutineSessionStartRequest(null))).StatusCode);
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/exercises/{wallSit.Id}", new ExerciseSetsRequest(1)));
        var tick = Assert.Single(report.TodaySessions[0].Completions);
        Assert.Equal((1, 45, 10m), (tick.SetsDone, tick.Value, tick.LoadKg));
        await Report(await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/exercises/{wallSit.Id}", new ExerciseSetsRequest(2)));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/exercises/{wallSit.Id}",
            new ExerciseSetsRequest(3))).StatusCode);
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/exercises/{bridge.Id}", new ExerciseSetsRequest(1)));
        Assert.False(report.CompletedToday);
        Assert.Equal(0, report.Streak);
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/exercises/{bridge.Id}", new ExerciseSetsRequest(2)));
        Assert.True(report.CompletedToday);
        Assert.True(report.TodaySessions[0].Finished);
        Assert.Equal((1, 1), (report.PassesToday, report.Streak));
        Assert.Equal("done", report.Calendar[^1].Status);

        // A finished pass is not reopened; its comment is noted.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/exercises/{bridge.Id}",
            new ExerciseSetsRequest(1))).StatusCode);
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/comment", new RoutineSessionCommentRequest("  Bien.  ")));
        Assert.Equal("Bien.", report.TodaySessions[0].Comment);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/comment",
            new RoutineSessionCommentRequest(new string('a', 1001)))).StatusCode);

        // The assistant's tick opens a second pass and records what was really done.
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{id}/completions",
            new ExerciseCompletionRequest(wallSit.Id, null, 60, 12.5m, null, null)));
        Assert.Equal(2, report.TodaySessions.Count);
        var second = report.TodaySessions[1];
        Assert.Equal((2, 60, 12.5m), (second.Completions[0].SetsDone, second.Completions[0].Value, second.Completions[0].LoadKg));
        Assert.Equal(1, report.PassesToday);
        report = await Report(await client.DeleteAsync($"/api/sport/routines/{id}/completions/{wallSit.Id}"));
        Assert.Empty(report.TodaySessions[1].Completions);
        report = await Report(await client.DeleteAsync($"/api/sport/routines/{id}/sessions/{second.Id}"));
        Assert.Single(report.TodaySessions);

        // A whole pass in one call.
        report = await Report(await client.PostAsJsonAsync($"/api/sport/routines/{id}/sessions/log", new RoutineSessionLogRequest(null, new TimeOnly(18, 30), "Le soir.",
            [new SessionExerciseRequest(wallSit.Id, null, null, 0), new SessionExerciseRequest(bridge.Id, null, 10, null)])));
        Assert.Equal(2, report.PassesToday);
        Assert.Null(report.TodaySessions.Single(s => s.Comment == "Le soir.").Completions.Single(c => c.ExerciseId == wallSit.Id).LoadKg);
        Assert.Equal(2, report.Calendar[^1].Passes);

        var progress = (await client.GetFromJsonAsync<ExerciseProgressDto>($"/api/sport/exercises/{wallSit.Id}/progress?weeks=2"))!;
        Assert.Equal((45, 10m, 1), (progress.BestValue, progress.BestLoadKg, progress.DaysDone));
        Assert.Equal(2, progress.Recent.Count);

        // The feeling of a pass and the profile's feeling defaults are gone.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sport/settings")).StatusCode);
        Assert.True((await client.PutAsJsonAsync($"/api/sport/routines/{id}/sessions/{pass}/feeling", new { effort = 7 })).StatusCode
            is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

        // A done exercise keeps its measure and cannot be archived while a routine uses it.
        var listed = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/sport/exercises"))!;
        Assert.True(listed.Single(e => e.Id == wallSit.Id).MeasureLocked);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sport/exercises/{wallSit.Id}",
            new ExerciseRequest("Chaise", null, "reps", 10))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/sport/exercises/{wallSit.Id}")).StatusCode);

        // Done or not, an exercise can become per side: its routine lines say so, the target applies to each side.
        var perSide = await client.PutAsJsonAsync($"/api/sport/exercises/{wallSit.Id}", new ExerciseRequest("Chaise", null, "hold", 10, PerSide: true));
        Assert.True((await perSide.Content.ReadFromJsonAsync<ExerciseDto>())!.PerSide);
        var routines = (await client.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines"))!;
        Assert.True(routines.Single(r => r.Routine.Id == id).Routine.Lines.Single(line => line.ExerciseId == wallSit.Id).PerSide);

        // The routine changes from today: same day, the version is edited in place.
        report = await Report(await client.PutAsJsonAsync($"/api/sport/routines/{id}", new RoutineRequest(null, "Tendons du matin",
            [1, 2, 3, 4, 5, 6, 7], 0, [new RoutineLineRequest(wallSit.Id, 3, 50, 12)])));
        Assert.Equal("Tendons du matin", report.Routine.Name);
        Assert.Equal((3, 50, 12m), (report.Routine.Lines[0].Sets, report.Routine.Lines[0].Target, report.Routine.Lines[0].LoadKg));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/exercises/{bridge.Id}")).StatusCode);

        var today = report.Today;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/sport/routines/{id}/sessions",
            new RoutineSessionStartRequest(today.AddDays(2)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sport/routines",
            new RoutineRequest(null, "Archivé", null, 0, [new RoutineLineRequest(bridge.Id, 1, 10, null)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sport/routines",
            new RoutineRequest(null, "Jour inconnu", [8], 0, [new RoutineLineRequest(wallSit.Id, 1, 10, null)]))).StatusCode);

        // An exercise is also done alone, outside any routine; it joins the exercise's history.
        var alone = await client.PostAsJsonAsync($"/api/sport/exercises/{wallSit.Id}/logs", new ExerciseLogRequest(2, 40));
        Assert.True(alone.IsSuccessStatusCode, await alone.Content.ReadAsStringAsync());
        var log = (await alone.Content.ReadFromJsonAsync<ExerciseCompletionDto>())!;
        Assert.Equal((null, 2, 40, 10m), (log.SessionId, log.SetsDone, log.Value, log.LoadKg));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/sport/exercises/{wallSit.Id}/logs",
            new ExerciseLogRequest(1, 40, null, today.AddDays(1)))).StatusCode);

        // Deleting a routine deletes its passes, never the exercises done: they stay without routine or pass.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/routines/{id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/sport/routines/{id}")).StatusCode);
        var kept = (await client.GetFromJsonAsync<ExerciseProgressDto>($"/api/sport/exercises/{wallSit.Id}/progress"))!;
        Assert.Equal(3, kept.Recent.Count);
        Assert.All(kept.Recent, done => Assert.Null(done.SessionId));
        Assert.Equal(2, (await client.GetFromJsonAsync<ExerciseProgressDto>($"/api/sport/exercises/{bridge.Id}/progress"))!.Recent.Count);

        // What stays out of a pass is deleted from the exercise.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/exercises/{wallSit.Id}/logs/{log.Id}")).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<ExerciseProgressDto>($"/api/sport/exercises/{wallSit.Id}/progress"))!.Recent.Count);
    }

    [Fact]
    public async Task A_tick_in_a_pass_is_not_deleted_as_an_exercise_done_alone()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var plank = await Exercise(client, "Planche", "hold", null);
        var routine = (await Report(await client.PostAsJsonAsync("/api/sport/routines",
            new RoutineRequest(null, "Dos", null, 0, [new RoutineLineRequest(plank.Id, 1, 30, null)])))).Routine;
        await Report(await client.PutAsJsonAsync($"/api/sport/routines/{routine.Id}/completions",
            new ExerciseCompletionRequest(plank.Id, null, null, null, null, null)));
        var tick = Assert.Single((await client.GetFromJsonAsync<ExerciseProgressDto>($"/api/sport/exercises/{plank.Id}/progress"))!.Recent);
        Assert.NotNull(tick.SessionId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/sport/exercises/{plank.Id}/logs/{tick.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_exercise_image_is_stored_and_served_only_to_its_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var exercise = await Exercise(client, "Chaise contre le mur", "hold", 10);
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
        var exercise = await Exercise(client, "Gainage", "hold", null);
        var lines = new[] { new RoutineLineRequest(exercise.Id, 2, 30, null) };
        var routine = (await Report(await client.PostAsJsonAsync("/api/sport/routines", new RoutineRequest(null, "Dos", null, 0, lines)))).Routine;

        Assert.Empty((await other.GetFromJsonAsync<List<ExerciseDto>>("/api/sport/exercises?includeArchived=true"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines"))!);
        var log = (await (await client.PostAsJsonAsync($"/api/sport/exercises/{exercise.Id}/logs", new ExerciseLogRequest(1, 30)))
            .Content.ReadFromJsonAsync<ExerciseCompletionDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/sport/exercises/{exercise.Id}/logs", new ExerciseLogRequest(1, 30))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/exercises/{exercise.Id}/logs/{log.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/exercises/{exercise.Id}",
            new ExerciseRequest("Volé", null, "hold", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/exercises/{exercise.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/sport/exercises/{exercise.Id}/progress")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/routines/{routine.Id}", new RoutineRequest(null, "Volée", null, 0, lines))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/routines/{routine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/routines/{routine.Id}/completions",
            new ExerciseCompletionRequest(exercise.Id, null, null, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/routines/{routine.Id}/completions/{exercise.Id}")).StatusCode);
        var pass = Assert.Single((await Report(await client.PostAsJsonAsync($"/api/sport/routines/{routine.Id}/sessions",
            new RoutineSessionStartRequest(null)))).TodaySessions).Id;
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/sport/routines/{routine.Id}/sessions", new RoutineSessionStartRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/routines/{routine.Id}/sessions/{pass}/exercises/{exercise.Id}",
            new ExerciseSetsRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/routines/{routine.Id}/sessions/{pass}/comment",
            new RoutineSessionCommentRequest("Volé"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/routines/{routine.Id}/sessions/{pass}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/sport/routines/{routine.Id}/sessions/log",
            new RoutineSessionLogRequest(null, null, null, [new SessionExerciseRequest(exercise.Id, null, null, null)]))).StatusCode);

        // Another household's exercise or profile is never accepted.
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/sport/routines", new RoutineRequest(null, "Dos", null, 0, lines))).StatusCode);
        var theirs = await Exercise(other, "Superman", "hold", null);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/sport/routines",
            new RoutineRequest(routine.MemberProfileId, "Dos", null, 0, [new RoutineLineRequest(theirs.Id, 1, 30, null)]))).StatusCode);

        Assert.Single((await client.GetFromJsonAsync<List<RoutineReportDto>>("/api/sport/routines"))!);
    }
}
