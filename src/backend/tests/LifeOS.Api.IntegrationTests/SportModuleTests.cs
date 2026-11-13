using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Sports;
using LifeOS.Application.Sports.Contracts;

namespace LifeOS.Api.IntegrationTests;

/// <summary>Roadmap step 2 (ADR 0013): sessions, weekly goals and daily challenges, per household and profile.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SportModuleTests(PostgresContainerFixture postgres)
{
    private static SportSessionRequest Run(Guid? template = null, int? calories = 520) =>
        new(null, null, "running", template, 46, 8.2m, calories, 7, 148, 171, "Fractionné");

    [Fact]
    public async Task Logged_sessions_fill_the_week_report_and_its_goals()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var catalog = (await client.GetFromJsonAsync<SportCatalogDto>("/api/sport/catalog"))!;
        Assert.Contains(catalog.Sports, s => s.Code == "crossfit" && !s.HasDistance);

        var footing = await (await client.PostAsJsonAsync("/api/sport-templates", new SportTemplateRequest("Footing 45 min", "running", 45, 8, 500)))
            .Content.ReadFromJsonAsync<SportTemplateDto>();
        (await client.PostAsJsonAsync("/api/sport/goals", new WeeklyGoalRequest(null, "template", footing!.Id, null, null, 2))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/sport/goals", new WeeklyGoalRequest(null, "volume", null, null, "kcal", 1500))).EnsureSuccessStatusCode();

        var created = await client.PostAsJsonAsync("/api/sport/sessions", Run(footing.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var session = (await created.Content.ReadFromJsonAsync<SportSessionDto>())!;
        Assert.Equal("Footing 45 min", session.TemplateName);
        (await client.PostAsJsonAsync("/api/sport/sessions", new SportSessionRequest(null, null, "crossfit", null, 60, null, null, 6, null, null, "Cindy")))
            .EnsureSuccessStatusCode();

        var week = Assert.Single((await client.GetFromJsonAsync<List<WeekReportDto>>("/api/sport/weeks"))!);
        Assert.Equal(2, week.SessionCount);
        Assert.Equal(520, week.Calories);
        Assert.False(week.CaloriesComplete);
        Assert.Equal(["Footing 45 min ×2", "1500 kcal"], week.Goals.Select(g => g.Goal.Label));
        Assert.Equal([1m, 520m], week.Goals.Select(g => g.Achieved));

        var update = await client.PutAsJsonAsync($"/api/sport/sessions/{session.Id}", Run(footing.Id, calories: 600));
        Assert.Equal(600, (await update.Content.ReadFromJsonAsync<SportSessionDto>())!.Calories);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/sessions/{session.Id}")).StatusCode);
        Assert.Equal(1, Assert.Single((await client.GetFromJsonAsync<List<WeekReportDto>>("/api/sport/weeks"))!).SessionCount);
    }

    [Fact]
    public async Task Sets_add_up_in_a_daily_challenge_until_the_target_is_reached()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var challenge = await (await client.PostAsJsonAsync("/api/sport/challenges", new DailyChallengeRequest(null, "Pompes", "reps", 100, 1)))
            .Content.ReadFromJsonAsync<DailyChallengeDto>();

        await client.PostAsJsonAsync($"/api/sport/challenges/{challenge!.Id}/entries", new ChallengeEntryRequest(60, null));
        var report = (await (await client.PostAsJsonAsync($"/api/sport/challenges/{challenge.Id}/entries", new ChallengeEntryRequest(45, null)))
            .Content.ReadFromJsonAsync<ChallengeReportDto>())!;
        Assert.Equal(105, report.TodayTotal);
        Assert.True(report.ReachedToday);
        Assert.Equal(1, report.Streak);
        Assert.Equal(1_000, report.NextMilestone);

        var undone = (await (await client.DeleteAsync($"/api/sport/challenges/{challenge.Id}/entries/{report.TodayEntries[^1].Id}"))
            .Content.ReadFromJsonAsync<ChallengeReportDto>())!;
        Assert.Equal(60, undone.TodayTotal);
        Assert.Equal(0, undone.Streak);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/sport/challenges/{challenge.Id}/entries",
            new ChallengeEntryRequest(10, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/sport/challenges/{challenge.Id}",
            new DailyChallengeRequest(null, "Pompes", "minutes", 100, 1))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sport/challenges/{challenge.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ChallengeReportDto>>("/api/sport/challenges"))!);
        Assert.Single((await client.GetFromJsonAsync<List<ChallengeReportDto>>("/api/sport/challenges?includeStopped=true"))!);
    }

    [Fact]
    public async Task Another_household_sees_and_changes_none_of_it_and_cannot_point_to_it()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var template = (await (await client.PostAsJsonAsync("/api/sport-templates", new SportTemplateRequest("Footing", "running", 45, 8, 500)))
            .Content.ReadFromJsonAsync<SportTemplateDto>())!;
        var session = (await (await client.PostAsJsonAsync("/api/sport/sessions", Run(template.Id))).Content.ReadFromJsonAsync<SportSessionDto>())!;
        var goal = (await (await client.PostAsJsonAsync("/api/sport/goals", new WeeklyGoalRequest(null, "sessions", null, null, null, 3)))
            .Content.ReadFromJsonAsync<WeeklyGoalDto>())!;
        var challenge = (await (await client.PostAsJsonAsync("/api/sport/challenges", new DailyChallengeRequest(null, "Pompes", "reps", 100, 0)))
            .Content.ReadFromJsonAsync<DailyChallengeDto>())!;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Empty((await other.GetFromJsonAsync<List<SportSessionDto>>($"/api/sport/sessions?from={today.AddDays(-7):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<WeeklyGoalDto>>("/api/sport/goals"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<ChallengeReportDto>>("/api/sport/challenges"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<SportDayDto>>($"/api/sport/days?from={today.AddDays(-7):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"))!);
        Assert.Equal(0, Assert.Single((await other.GetFromJsonAsync<List<WeekReportDto>>("/api/sport/weeks"))!).SessionCount);

        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/sport/sessions/{session.Id}", Run())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/goals/{goal.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/sport/challenges/{challenge.Id}/entries", new ChallengeEntryRequest(10, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/sport/challenges/{challenge.Id}")).StatusCode);

        // Another household's template or profile is never accepted.
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/sport/sessions", Run(template.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/sport/goals",
            new WeeklyGoalRequest(null, "template", template.Id, null, null, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/sport/sessions", Run() with { MemberProfileId = session.MemberProfileId })).StatusCode);

        Assert.Single((await client.GetFromJsonAsync<List<ChallengeReportDto>>("/api/sport/challenges"))!);
    }
}
