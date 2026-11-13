using System.Text.Json;
using LifeOS.Api.Sports;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LifeOS.Api.IntegrationTests;

/// <summary>Roadmap step 2: the Sport module is usable from a Claude conversation, for its household only.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AssistantSportToolsTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task The_assistant_logs_sessions_goals_and_challenge_sets_of_its_household_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        await using var mcp = await Connect(factory, Guid.NewGuid());
        await using var neighbour = await Connect(factory, Guid.NewGuid());

        var template = await Call(mcp, "create_sport_template", new() { ["name"] = "Footing 45 min", ["sport"] = "running", ["durationMinutes"] = 45, ["calories"] = 500, ["distanceKm"] = 8 });
        var templateId = template.GetProperty("id").GetString();
        await Call(mcp, "create_weekly_goal", new() { ["kind"] = "template", ["templateId"] = templateId, ["target"] = 2 });
        await Call(mcp, "log_sport_session", new() { ["sport"] = "running", ["durationMinutes"] = 46, ["templateId"] = templateId, ["calories"] = 520, ["feeling"] = 7 });

        var week = Assert.Single((await Call(mcp, "get_sport_weeks", new())).EnumerateArray());
        Assert.Equal(1, week.GetProperty("sessionCount").GetInt32());
        Assert.Equal(1, Assert.Single(week.GetProperty("goals").EnumerateArray()).GetProperty("achieved").GetDecimal());

        var challenge = await Call(mcp, "create_daily_challenge", new() { ["name"] = "Pompes", ["unit"] = "reps", ["dailyTarget"] = 100, ["jokersPerWeek"] = 1 });
        var report = await Call(mcp, "add_challenge_entry", new() { ["challengeId"] = challenge.GetProperty("id").GetString(), ["quantity"] = 30 });
        Assert.Equal(30, report.GetProperty("todayTotal").GetInt32());

        Assert.Contains("Sport inconnu", await Error(mcp, "log_sport_session", new() { ["sport"] = "yoga", ["durationMinutes"] = 30 }));
        Assert.Contains("introuvable", await Error(neighbour, "add_challenge_entry", new() { ["challengeId"] = challenge.GetProperty("id").GetString(), ["quantity"] = 30 }));
        Assert.Empty((await Call(neighbour, "list_daily_challenges", new())).EnumerateArray());
        Assert.Empty((await Call(neighbour, "list_sport_templates", new())).EnumerateArray());
    }

    private static async Task<McpClient> Connect(WebApplicationFactory<Program> factory, Guid user)
    {
        var http = factory.CreateClient().AsUser(user);
        return await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            http));
    }

    private static async Task<JsonElement> Call(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        var text = Text(result);
        Assert.True(result.IsError != true, $"{tool} failed: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> Error(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        Assert.True(result.IsError, $"{tool} should have failed: {Text(result)}");
        return Text(result);
    }

    private static Dictionary<string, object?> Json(Dictionary<string, object?> arguments) =>
        arguments.ToDictionary(pair => pair.Key, pair => (object?)JsonSerializer.SerializeToElement(pair.Value));

    private static string Text(CallToolResult result) =>
        string.Join("", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
