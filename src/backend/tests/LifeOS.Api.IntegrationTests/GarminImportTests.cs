using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Authentication;
using LifeOS.Api.Health;
using LifeOS.Api.Households;
using LifeOS.Api.Sports;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Households;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0019: garmin-sync writes with an import token; the user reads and drives the connection.</summary>
[Collection(PostgresCollection.Name)]
public sealed class GarminImportTests(PostgresContainerFixture postgres)
{
    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
    private static readonly DateTimeOffset Start = new(Yesterday.ToDateTime(new TimeOnly(10, 14)), TimeSpan.Zero);

    private static GarminActivityRequest Activity(string type = "running", double minutes = 28, DateTimeOffset? start = null, int? rpe = 70) =>
        new(type, "Course à pied", start ?? Start, minutes * 60, minutes * 60 + 40, 4680, 41, 331, 133, 150, 2.78, 2.7, 0.8, rpe, 75,
            [180, 540, 840, 120, 0], JsonDocument.Parse("{\"activityId\":1}").RootElement);

    private static async Task<HttpClient> ServiceAsync(LifeOSApiFactory factory, HttpClient user)
    {
        var created = await user.PostAsJsonAsync("/api/import-tokens", new ImportTokenRequest(null, null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var token = (await created.Content.ReadFromJsonAsync<CreatedImportTokenDto>())!;
        Assert.StartsWith(ImportToken.Prefix, token.Token);
        var service = factory.CreateClient();
        service.DefaultRequestHeaders.Add(ImportTokenAuthentication.HeaderName, token.Token);
        return service;
    }

    private static async Task<List<SportSessionDto>> SessionsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<SportSessionDto>>($"/api/sport/sessions?from={Yesterday.AddDays(-3):yyyy-MM-dd}&to={Yesterday.AddDays(2):yyyy-MM-dd}"))!;

    [Fact]
    public async Task Activities_become_sessions_that_follow_garmin_and_keep_the_notes()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var user = factory.CreateClient().AsUser(Guid.NewGuid());
        using var service = await ServiceAsync(factory, user);

        var put = await service.PutAsJsonAsync("/api/import/garmin/activities/101", Activity());
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var session = Assert.Single(await SessionsAsync(user));
        Assert.Equal("garmin", session.Source);
        Assert.Equal("running", session.Sport);
        Assert.Equal(4.68m, session.DistanceKm);
        Assert.Equal(7, session.Effort);
        Assert.Equal("strong", session.Feel);
        Assert.Equal(28, session.DurationMinutes);
        Assert.Equal(2.7m, session.AerobicTrainingEffect);

        // The user adds notes and changes the sport; Garmin then changes its values.
        var mine = new SportSessionRequest(null, null, null, null, "walking", null, null, null, null, null, null, null, null, "Avec le chien");
        (await user.PutAsJsonAsync($"/api/sport/sessions/{session.Id}", mine)).EnsureSuccessStatusCode();
        (await service.PutAsJsonAsync("/api/import/garmin/activities/101", Activity(minutes: 30))).EnsureSuccessStatusCode();
        session = Assert.Single(await SessionsAsync(user));
        Assert.Equal("walking", session.Sport);
        Assert.True(session.SportOverridden);
        Assert.Equal("Avec le chien", session.Notes);
        Assert.Equal(30, session.DurationMinutes);

        // HIIT is crossfit, every type is imported.
        (await service.PutAsJsonAsync("/api/import/garmin/activities/102", Activity("hiit", start: Start.AddHours(3)))).EnsureSuccessStatusCode();
        (await service.PutAsJsonAsync("/api/import/garmin/activities/103", Activity("skate_skiing_ws", start: Start.AddHours(6)))).EnsureSuccessStatusCode();
        var all = await SessionsAsync(user);
        Assert.Contains(all, s => s.GarminActivityId == 102 && s.Sport == "crossfit" && s.DistanceKm is null);
        Assert.Contains(all, s => s.GarminActivityId == 103 && s.Sport == "other" && s.GarminType == "skate_skiing_ws");

        // An activity deleted in Garmin Connect is deleted here; an empty list deletes nothing.
        Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsJsonAsync("/api/import/garmin/activities/reconcile", new GarminReconcileRequest([]))).StatusCode);
        var reconciled = await (await service.PostAsJsonAsync("/api/import/garmin/activities/reconcile", new GarminReconcileRequest([101, 103])))
            .Content.ReadFromJsonAsync<GarminReconcileDto>();
        Assert.Equal(1, reconciled!.Deleted);
        Assert.Equal([101L, 103L], (await SessionsAsync(user)).Select(s => s.GarminActivityId!.Value).Order());
    }

    [Fact]
    public async Task A_manual_session_of_the_same_sport_and_time_merges_with_the_activity()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var user = factory.CreateClient().AsUser(Guid.NewGuid());
        using var service = await ServiceAsync(factory, user);
        var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(Start, paris).DateTime);
        (await user.PostAsJsonAsync("/api/sport/sessions", new SportSessionRequest(null, Yesterday, local.AddMinutes(-5), local.AddMinutes(25),
            "running", null, null, null, null, null, null, null, null, "Fractionné"))).EnsureSuccessStatusCode();
        (await user.PostAsJsonAsync("/api/sport/sessions", new SportSessionRequest(null, Yesterday, local.AddMinutes(-5), local.AddMinutes(25),
            "cycling", null, null, null, null, null, null, null, null, null))).EnsureSuccessStatusCode();

        (await service.PutAsJsonAsync("/api/import/garmin/activities/201", Activity())).EnsureSuccessStatusCode();
        var sessions = await SessionsAsync(user);
        Assert.Equal(2, sessions.Count);
        var run = Assert.Single(sessions, s => s.Sport == "running");
        Assert.Equal("garmin", run.Source);
        Assert.Equal("Fractionné", run.Notes);
        Assert.Equal("manual", Assert.Single(sessions, s => s.Sport == "cycling").Source);
    }

    [Fact]
    public async Task Vo2max_and_wellness_are_upserted_by_day_and_stay_private()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var user = factory.CreateClient().AsUser(Guid.NewGuid());
        using var stranger = factory.CreateClient().AsUser(Guid.NewGuid());
        using var service = await ServiceAsync(factory, user);

        var vo2 = new Vo2MaxImportRequest([new Vo2MaxInput(Yesterday.AddDays(-30), "running", 48.0m), new Vo2MaxInput(Yesterday, "running", 49.2m)]);
        Assert.Equal(HttpStatusCode.NoContent, (await service.PostAsJsonAsync("/api/import/garmin/vo2max", vo2)).StatusCode);
        (await service.PostAsJsonAsync("/api/import/garmin/vo2max", new Vo2MaxImportRequest([new Vo2MaxInput(Yesterday, "running", 49.3m)]))).EnsureSuccessStatusCode();
        var curve = (await user.GetFromJsonAsync<List<Vo2MaxDto>>("/api/sport/vo2max"))!;
        Assert.Equal([48.0m, 49.3m], curve.Select(m => m.Value));
        Assert.Empty((await stranger.GetFromJsonAsync<List<Vo2MaxDto>>("/api/sport/vo2max"))!);

        var night = new WellnessInput(Yesterday, Start.AddHours(-12), Start.AddHours(-5), 22380, 5220, 14820, 2340, 3360, 68, "fair",
            49, 43, 42, 37, 47, "BALANCED", 12.5m, 95m, 62, 80, 20, 30, 4210, JsonDocument.Parse("{\"sleep\":{\"dailySleepDTO\":{}}}").RootElement);
        Assert.Equal(HttpStatusCode.NoContent, (await service.PostAsJsonAsync("/api/import/garmin/wellness", new WellnessImportRequest([night]))).StatusCode);
        (await service.PostAsJsonAsync("/api/import/garmin/wellness", new WellnessImportRequest([night with { SleepScore = 70 }]))).EnsureSuccessStatusCode();
        var days = (await user.GetFromJsonAsync<List<WellnessDto>>($"/api/health/wellness?from={Yesterday.AddDays(-1):yyyy-MM-dd}"))!;
        var day = Assert.Single(days);
        Assert.Equal(70, day.SleepScore);
        Assert.Equal("FAIR", day.SleepQuality);
        Assert.Equal("balanced", day.HrvStatus);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/health/wellness/{Yesterday:yyyy-MM-dd}/raw")).StatusCode);
        Assert.Empty((await stranger.GetFromJsonAsync<List<WellnessDto>>($"/api/health/wellness?from={Yesterday.AddDays(-1):yyyy-MM-dd}"))!);
    }

    [Fact]
    public async Task The_token_only_opens_the_import_endpoints_and_can_be_revoked()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var user = factory.CreateClient().AsUser(Guid.NewGuid());
        using var service = await ServiceAsync(factory, user);

        Assert.Equal(HttpStatusCode.Unauthorized, (await service.GetAsync("/api/sport/weeks")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await service.GetAsync("/api/health/wellness")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await user.PostAsJsonAsync("/api/import/garmin/vo2max", new Vo2MaxImportRequest([]))).StatusCode);

        var token = Assert.Single((await user.GetFromJsonAsync<List<ImportTokenDto>>("/api/import-tokens"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/import-tokens/{token.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await service.PostAsync("/api/import/garmin/commands", null)).StatusCode);
    }

    [Fact]
    public async Task The_mfa_code_goes_from_the_user_to_the_service_once()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var user = factory.CreateClient().AsUser(Guid.NewGuid());
        using var service = await ServiceAsync(factory, user);

        Assert.Equal("never", (await user.GetFromJsonAsync<GarminStatusDto>("/api/garmin"))!.State);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync("/api/garmin/code", new GarminCodeRequest("123456"))).StatusCode);

        var asked = await (await service.PostAsJsonAsync("/api/import/garmin/status", new GarminReportRequest("mfa_requested", "Code envoyé par email", null, null)))
            .Content.ReadFromJsonAsync<GarminStatusDto>();
        Assert.True(asked!.WaitsForCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync("/api/garmin/code", new GarminCodeRequest("12ab"))).StatusCode);
        (await user.PostAsJsonAsync("/api/garmin/code", new GarminCodeRequest("123 456"))).EnsureSuccessStatusCode();
        (await user.PostAsync("/api/garmin/sync", null)).EnsureSuccessStatusCode();

        var commands = (await (await service.PostAsync("/api/import/garmin/commands", null)).Content.ReadFromJsonAsync<GarminCommands>())!;
        Assert.Equal("123456", commands.MfaCode);
        Assert.True(commands.Sync);
        var again = (await (await service.PostAsync("/api/import/garmin/commands", null)).Content.ReadFromJsonAsync<GarminCommands>())!;
        Assert.Null(again.MfaCode);
        Assert.False(again.Sync);

        // The service sends Paris times; they are stored as UTC instants.
        var next = new DateTimeOffset(2026, 11, 23, 19, 0, 0, TimeSpan.FromHours(1));
        (await service.PostAsJsonAsync("/api/import/garmin/status", new GarminReportRequest("ok", null, "1 activité, 1 nuit", next))).EnsureSuccessStatusCode();
        var status = (await user.GetFromJsonAsync<GarminStatusDto>("/api/garmin"))!;
        Assert.Equal("ok", status.State);
        Assert.Equal("1 activité, 1 nuit", status.LastSuccessSummary);
        Assert.False(status.WaitsForCode);
        Assert.Equal(next, status.NextSyncAt);
    }

    [Fact]
    public async Task The_assistant_reads_the_garmin_data_of_its_account()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var userId = Guid.NewGuid();
        using var user = factory.CreateClient().AsUser(userId);
        using var service = await ServiceAsync(factory, user);
        (await service.PostAsJsonAsync("/api/import/garmin/vo2max", new Vo2MaxImportRequest([new Vo2MaxInput(Yesterday, "running", 49.2m)]))).EnsureSuccessStatusCode();
        (await service.PostAsJsonAsync("/api/import/garmin/status", new GarminReportRequest("ok", null, "1 activité", null))).EnsureSuccessStatusCode();

        var http = factory.CreateClient().AsUser(userId);
        await using var mcp = await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp }, http));
        var tools = (await mcp.ListToolsAsync()).Select(tool => tool.Name).ToList();
        Assert.Contains("get_sleep_and_recovery", tools);
        Assert.Contains("get_sport_session", tools);
        string Text(CallToolResult result) => string.Join("", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        Assert.Contains("49.2", Text(await mcp.CallToolAsync("list_vo2max")));
        Assert.Contains("\"ok\"", Text(await mcp.CallToolAsync("get_garmin_status")));
    }
}
