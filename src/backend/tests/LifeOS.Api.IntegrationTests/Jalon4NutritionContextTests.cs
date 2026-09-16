using System.Net;
using System.Net.Http.Json;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Integration tests for Jalon 4 nutrition and sports context.
/// Validates deterministic calorie calculations and household isolation.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Jalon4NutritionContextTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private LifeOSApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new LifeOSApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    /// <summary>
    /// Test that a user configuration can be created and retrieved.
    /// </summary>
    [Fact]
    public async Task UserConfiguration_CreateAndRetrieve_Success()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create user configuration
        var createRequest = new
        {
            dailyBaseEnergyKcal = 2000m,
            targetNetDeficitKcal = 500m,
            targetProteinG = 150m,
            targetCarbsG = 200m,
            targetFatsG = 70m
        };

        var createResult = await client.PostAsJsonAsync("/api/nutrition/configuration", createRequest);
        createResult.EnsureSuccessStatusCode();

        var config = await createResult.Content.ReadFromJsonAsync<dynamic>();

        // Assert: Verify created configuration
        Assert.NotNull(config);
        Assert.Equal(2000m, (decimal)config!.dailyBaseEnergyKcal);
        Assert.Equal(500m, (decimal)config.targetNetDeficitKcal);

        // Act: Retrieve configuration
        var getResult = await client.GetAsync("/api/nutrition/configuration");
        getResult.EnsureSuccessStatusCode();

        var retrievedConfig = await getResult.Content.ReadFromJsonAsync<dynamic>();

        // Assert: Verify retrieved configuration
        Assert.NotNull(retrievedConfig);
        Assert.Equal(2000m, (decimal)retrievedConfig!.dailyBaseEnergyKcal);
    }

    /// <summary>
    /// Test that activity sessions can be created for a day.
    /// </summary>
    [Fact]
    public async Task ActivitySessions_CreateAndList_Success()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Setup: Create a week with day plans
        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            status = "draft"
        };

        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();

        // Get weeks and extract first day plan
        var weeksResult = await client.GetAsync("/api/weeks");
        weeksResult.EnsureSuccessStatusCode();

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<dynamic>>();
        Assert.NotNull(weeks);
        Assert.NotEmpty(weeks);

        var dayPlans = (List<dynamic>)weeks[0].dayPlans;
        var dayPlanId = (Guid)dayPlans[0].id;

        // Act: Create first activity session
        var run = await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayPlanId}", new
        {
            type = "run",
            intensity = "moderate",
            durationMinutes = 30,
            estimatedEnergyKcal = 300m
        });
        run.EnsureSuccessStatusCode();

        // Act: Create second activity session
        var strength = await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayPlanId}", new
        {
            type = "strength",
            intensity = "high",
            durationMinutes = 45,
            estimatedEnergyKcal = 250m
        });
        strength.EnsureSuccessStatusCode();

        // Act: Get activity sessions for the day
        var getResult = await client.GetAsync($"/api/activity-sessions/day/{dayPlanId}");
        getResult.EnsureSuccessStatusCode();

        var sessions = await getResult.Content.ReadFromJsonAsync<List<dynamic>>();

        // Assert: Verify both sessions exist
        Assert.NotNull(sessions);
        Assert.Equal(2, sessions.Count);
        Assert.Equal("run", (string)sessions[0].type);
        Assert.Equal("strength", (string)sessions[1].type);
    }

    /// <summary>
    /// Test deterministic nutrition target calculation:
    /// daily_food_target = base_energy + activity_energy - deficit_goal
    /// </summary>
    [Fact]
    public async Task DailyNutritionTarget_CalculationIsCorrect()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Setup: Create user configuration
        await client.PostAsJsonAsync("/api/nutrition/configuration", new
        {
            dailyBaseEnergyKcal = 2000m,
            targetNetDeficitKcal = 500m,
            targetProteinG = 150m,
            targetCarbsG = 200m,
            targetFatsG = 70m
        });

        // Setup: Create a week with day plans
        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            status = "draft"
        };

        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();

        // Get weeks and extract first day plan
        var weeksResult = await client.GetAsync("/api/weeks");
        weeksResult.EnsureSuccessStatusCode();

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<dynamic>>();
        Assert.NotNull(weeks);

        var dayPlans = (List<dynamic>)weeks[0].dayPlans;
        var dayPlanId = (Guid)dayPlans[0].id;

        // Create activity session
        await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayPlanId}", new
        {
            type = "run",
            intensity = "moderate",
            durationMinutes = 30,
            estimatedEnergyKcal = 400m
        });

        // Act: Get nutrition target calculation
        var result = await client.GetAsync($"/api/nutrition/calculations/day/{dayPlanId}");
        result.EnsureSuccessStatusCode();

        var target = await result.Content.ReadFromJsonAsync<dynamic>();

        // Assert: Verify deterministic calculation
        // daily_food_target = 2000 + 400 - 500 = 1900
        Assert.NotNull(target);
        Assert.Equal(2000m, (decimal)target!.dailyBaseEnergyKcal);
        Assert.Equal(400m, (decimal)target.activityEnergyKcal);
        Assert.Equal(500m, (decimal)target.targetNetDeficitKcal);
        Assert.Equal(1900m, (decimal)target.dailyFoodTargetKcal);
    }

    /// <summary>
    /// Test household isolation: a user should not access another household's activity sessions.
    /// </summary>
    [Fact]
    public async Task HouseholdIsolation_CannotAccessAnotherHouseholdData_Forbidden()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        using var client1 = _factory.CreateClient().AsUser(user1);
        using var client2 = _factory.CreateClient().AsUser(user2);

        // Setup: User1 creates a week with day plans
        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            status = "draft"
        };

        var weekResult = await client1.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();

        var weeksResult = await client1.GetAsync("/api/weeks");
        weeksResult.EnsureSuccessStatusCode();

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<dynamic>>();
        Assert.NotNull(weeks);

        var dayPlans = (List<dynamic>)weeks[0].dayPlans;
        var dayPlanId = (Guid)dayPlans[0].id;

        // Act: User2 tries to access User1's day plan - should be forbidden
        var result = await client2.GetAsync($"/api/activity-sessions/day/{dayPlanId}");

        // Assert: Should be Forbidden (403)
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    /// <summary>
    /// Test that activity session can be updated.
    /// </summary>
    [Fact]
    public async Task ActivitySession_Update_Success()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Setup: Create a week with day plans
        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            status = "draft"
        };

        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();

        var weeksResult = await client.GetAsync("/api/weeks");
        weeksResult.EnsureSuccessStatusCode();

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<dynamic>>();
        var dayPlans = (List<dynamic>)weeks![0].dayPlans;
        var dayPlanId = (Guid)dayPlans[0].id;

        // Create activity session
        var createResult = await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayPlanId}", new
        {
            type = "run",
            intensity = "moderate",
            durationMinutes = 30,
            estimatedEnergyKcal = 300m
        });
        createResult.EnsureSuccessStatusCode();

        var sessionContent = await createResult.Content.ReadFromJsonAsync<dynamic>();
        var sessionId = (Guid)sessionContent!.id;

        // Act: Update session
        var updateResult = await client.PutAsJsonAsync($"/api/activity-sessions/{sessionId}", new
        {
            durationMinutes = 45,
            estimatedEnergyKcal = 450m
        });
        updateResult.EnsureSuccessStatusCode();

        var updatedSession = await updateResult.Content.ReadFromJsonAsync<dynamic>();

        // Assert: Verify update
        Assert.Equal(45, (int)updatedSession!.durationMinutes);
        Assert.Equal(450m, (decimal)updatedSession.estimatedEnergyKcal);
    }

    /// <summary>
    /// Test that activity session can be deleted.
    /// </summary>
    [Fact]
    public async Task ActivitySession_Delete_Success()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Setup: Create a week with day plans
        var weekRequest = new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            status = "draft"
        };

        var weekResult = await client.PostAsJsonAsync("/api/weeks", weekRequest);
        weekResult.EnsureSuccessStatusCode();

        var weeksResult = await client.GetAsync("/api/weeks");
        weeksResult.EnsureSuccessStatusCode();

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<dynamic>>();
        var dayPlans = (List<dynamic>)weeks![0].dayPlans;
        var dayPlanId = (Guid)dayPlans[0].id;

        // Create activity session
        var createResult = await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayPlanId}", new
        {
            type = "bike",
            intensity = "low",
            durationMinutes = 20,
            estimatedEnergyKcal = 150m
        });
        createResult.EnsureSuccessStatusCode();

        var sessionContent = await createResult.Content.ReadFromJsonAsync<dynamic>();
        var sessionId = (Guid)sessionContent!.id;

        // Act: Delete session
        var deleteResult = await client.DeleteAsync($"/api/activity-sessions/{sessionId}");

        // Assert: Verify deletion
        Assert.Equal(HttpStatusCode.NoContent, deleteResult.StatusCode);

        // Verify session is gone
        var getResult = await client.GetAsync($"/api/activity-sessions/day/{dayPlanId}");
        getResult.EnsureSuccessStatusCode();

        var sessions = await getResult.Content.ReadFromJsonAsync<List<dynamic>>();
        Assert.Empty(sessions);
    }
}
