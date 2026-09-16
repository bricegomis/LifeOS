using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Dtos;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Integration tests for Jalon 5 deterministic scenario generation.
/// Validates scenario generation, application, persistence, and household isolation.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Jalon5ScenariosTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private LifeOSApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new LifeOSApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    /// <summary>
    /// Test that scenarios can be generated for a week.
    /// </summary>
    [Fact]
    public async Task GenerateScenarios_WithValidObjectives_Success()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create a household
        var householdRequest = new { name = "Test Household" };
        var householdResult = await client.PostAsJsonAsync("/api/households", householdRequest);
        householdResult.EnsureSuccessStatusCode();

        // Create a week
        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        };
        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();
        var week = await weekResult.Content.ReadFromJsonAsync<WeekDto>();
        var weekId = week!.Id;

        // Generate scenarios
        var generateRequest = new
        {
            objectives = new[] { "nutritional_balance", "economy", "reduce_waste" }
        };
        var generateResult = await client.PostAsJsonAsync(
            $"/api/weeks/{weekId}/scenarios/generate",
            generateRequest);
        generateResult.EnsureSuccessStatusCode();

        var scenarios = await generateResult.Content.ReadFromJsonAsync<List<WeekScenarioResponse>>();

        // Assertions
        Assert.NotNull(scenarios);
        Assert.Equal(3, scenarios.Count);

        // Check that scenarios have different objectives
        var objectives = scenarios.Select(s => s.RankingObjective).ToHashSet();
        Assert.Contains("nutritional_balance", objectives);
        Assert.Contains("economy", objectives);
        Assert.Contains("reduce_waste", objectives);

        // Check that explanations are present
        foreach (var scenario in scenarios)
        {
            Assert.Equal(JsonValueKind.Object, scenario.Explanation.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(scenario.Explanation.GetProperty("textExplanation").GetString()));
        }
    }

    /// <summary>
    /// Test that scenarios can be retrieved for a week.
    /// </summary>
    [Fact]
    public async Task GetScenarios_ForExistingWeek_Success()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create a household
        var householdRequest = new { name = "Test Household" };
        var householdResult = await client.PostAsJsonAsync("/api/households", householdRequest);
        householdResult.EnsureSuccessStatusCode();

        // Create a week
        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        };
        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();
        var week = await weekResult.Content.ReadFromJsonAsync<WeekDto>();
        var weekId = week!.Id;

        // Generate scenarios
        var generateRequest = new
        {
            objectives = new[] { "nutritional_balance" }
        };
        await client.PostAsJsonAsync($"/api/weeks/{weekId}/scenarios/generate", generateRequest);

        // Retrieve scenarios
        var getResult = await client.GetAsync($"/api/weeks/{weekId}/scenarios");
        getResult.EnsureSuccessStatusCode();

        var scenarios = await getResult.Content.ReadFromJsonAsync<List<WeekScenarioResponse>>();

        // Assertions
        Assert.NotNull(scenarios);
        Assert.Single(scenarios);
        Assert.Equal("nutritional_balance", scenarios[0].RankingObjective);
        Assert.False(scenarios[0].Applied);
    }

    /// <summary>
    /// Test that scenarios are isolated by household (cannot see other household's scenarios).
    /// </summary>
    [Fact]
    public async Task Scenarios_AreIsolatedByHousehold()
    {
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        using var client1 = _factory.CreateClient().AsUser(user1Id);
        using var client2 = _factory.CreateClient().AsUser(user2Id);

        // User 1 creates a household and week
        var household1Request = new { name = "Household 1" };
        var household1Result = await client1.PostAsJsonAsync("/api/households", household1Request);
        household1Result.EnsureSuccessStatusCode();

        var week1Request = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        };
        var week1Result = await client1.PostAsJsonAsync("/api/weeks", week1Request);
        week1Result.EnsureSuccessStatusCode();
        var week1 = await week1Result.Content.ReadFromJsonAsync<WeekDto>();
        var week1Id = week1!.Id;

        // User 1 generates scenarios
        var generateRequest = new
        {
            objectives = new[] { "nutritional_balance" }
        };
        var generateResult = await client1.PostAsJsonAsync(
            $"/api/weeks/{week1Id}/scenarios/generate",
            generateRequest);
        generateResult.EnsureSuccessStatusCode();

        // User 2 creates their own household
        var household2Request = new { name = "Household 2" };
        var household2Result = await client2.PostAsJsonAsync("/api/households", household2Request);
        household2Result.EnsureSuccessStatusCode();

        // User 2 tries to access user 1's week scenarios (should fail)
        var accessResult = await client2.GetAsync($"/api/weeks/{week1Id}/scenarios");
        Assert.Equal(HttpStatusCode.NotFound, accessResult.StatusCode);
    }

    /// <summary>
    /// Test that applying a scenario marks it as applied.
    /// </summary>
    [Fact]
    public async Task ApplyScenario_MarksAsApplied()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create a household and week
        var householdRequest = new { name = "Test Household" };
        var householdResult = await client.PostAsJsonAsync("/api/households", householdRequest);
        householdResult.EnsureSuccessStatusCode();

        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        };
        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();
        var week = await weekResult.Content.ReadFromJsonAsync<WeekDto>();
        var weekId = week!.Id;

        // Generate scenarios
        var generateRequest = new
        {
            objectives = new[] { "economy" }
        };
        var generateResult = await client.PostAsJsonAsync(
            $"/api/weeks/{weekId}/scenarios/generate",
            generateRequest);
        generateResult.EnsureSuccessStatusCode();

        var scenarios = await generateResult.Content.ReadFromJsonAsync<List<WeekScenarioResponse>>();
        var scenarioId = scenarios![0].Id;

        // Apply scenario
        var applyResult = await client.PatchAsync(
            $"/api/weeks/{weekId}/scenarios/{scenarioId}/apply",
            null);
        Assert.Equal(HttpStatusCode.NoContent, applyResult.StatusCode);

        // Retrieve and verify it's marked as applied
        var getResult = await client.GetAsync($"/api/weeks/{weekId}/scenarios");
        getResult.EnsureSuccessStatusCode();

        var updatedScenarios = await getResult.Content.ReadFromJsonAsync<List<WeekScenarioResponse>>();
        Assert.NotNull(updatedScenarios);
        Assert.Single(updatedScenarios);
        Assert.True(updatedScenarios[0].Applied);
    }

    /// <summary>
    /// Test that scenarios persist across requests.
    /// </summary>
    [Fact]
    public async Task Scenarios_PersistAcrossRequests()
    {
        var supabaseUserId = Guid.NewGuid();

        // First request: Create household and week, generate scenarios
        {
            using var client = _factory.CreateClient().AsUser(supabaseUserId);

            var householdRequest = new { name = "Test Household" };
            var householdResult = await client.PostAsJsonAsync("/api/households", householdRequest);
            householdResult.EnsureSuccessStatusCode();

            var weekRequest = new
            {
                startsOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
            };
            var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
            weekResult.EnsureSuccessStatusCode();
            var week = await weekResult.Content.ReadFromJsonAsync<WeekDto>();
            var weekId = week!.Id;

            var generateRequest = new
            {
                objectives = new[] { "reduce_waste" }
            };
            var generateResult = await client.PostAsJsonAsync(
                $"/api/weeks/{weekId}/scenarios/generate",
                generateRequest);
            generateResult.EnsureSuccessStatusCode();
        }

        // Second request: Should still be able to retrieve scenarios
        {
            using var client = _factory.CreateClient().AsUser(supabaseUserId);

            var weeksResult = await client.GetAsync("/api/weeks");
            weeksResult.EnsureSuccessStatusCode();
            var weeks = await weeksResult.Content.ReadFromJsonAsync<List<WeekDto>>();
            Assert.NotNull(weeks);
            Assert.NotEmpty(weeks);

            var weekId = weeks[0].Id;

            var getResult = await client.GetAsync($"/api/weeks/{weekId}/scenarios");
            getResult.EnsureSuccessStatusCode();

            var scenarios = await getResult.Content.ReadFromJsonAsync<List<WeekScenarioResponse>>();
            Assert.NotNull(scenarios);
            Assert.Single(scenarios);
            Assert.Equal("reduce_waste", scenarios[0].RankingObjective);
        }
    }

    /// <summary>
    /// Test that scenario generation fails for non-existent week.
    /// </summary>
    [Fact]
    public async Task GenerateScenarios_ForNonExistentWeek_ReturnsNotFound()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        var generateRequest = new
        {
            objectives = new[] { "nutritional_balance" }
        };

        var generateResult = await client.PostAsJsonAsync(
            $"/api/weeks/{Guid.NewGuid()}/scenarios/generate",
            generateRequest);

        Assert.Equal(HttpStatusCode.NotFound, generateResult.StatusCode);
    }

    /// <summary>
    /// Test that scenario generation fails with empty objectives.
    /// </summary>
    [Fact]
    public async Task GenerateScenarios_WithEmptyObjectives_ReturnsBadRequest()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create household and week
        var householdRequest = new { name = "Test Household" };
        var householdResult = await client.PostAsJsonAsync("/api/households", householdRequest);
        householdResult.EnsureSuccessStatusCode();

        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        };
        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();
        var week = await weekResult.Content.ReadFromJsonAsync<WeekDto>();
        var weekId = week!.Id;

        // Try to generate with empty objectives
        var generateRequest = new { objectives = new List<string>() };
        var generateResult = await client.PostAsJsonAsync(
            $"/api/weeks/{weekId}/scenarios/generate",
            generateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, generateResult.StatusCode);
    }

    private sealed record WeekScenarioResponse(
        Guid Id,
        Guid WeekId,
        string RankingObjective,
        JsonElement Explanation,
        bool Applied,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
