using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Dtos;
using LifeOS.Application.Households;

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

        var config = await createResult.Content.ReadFromJsonAsync<UserConfigurationDto>();

        // Assert: Verify created configuration
        Assert.NotNull(config);
        Assert.Equal(2000m, config!.DailyBaseEnergyKcal);
        Assert.Equal(500m, config.TargetNetDeficitKcal);

        // Act: Retrieve configuration
        var getResult = await client.GetAsync("/api/nutrition/configuration");
        getResult.EnsureSuccessStatusCode();

        var retrievedConfig = await getResult.Content.ReadFromJsonAsync<UserConfigurationDto>();

        // Assert: Verify retrieved configuration
        Assert.NotNull(retrievedConfig);
        Assert.Equal(2000m, retrievedConfig!.DailyBaseEnergyKcal);
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

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<WeekDto>>();
        Assert.NotNull(weeks);
        Assert.NotEmpty(weeks);

        var dayPlanId = weeks[0].DayPlans[0].Id;

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

        var sessions = await getResult.Content.ReadFromJsonAsync<List<ActivitySessionDto>>();

        // Assert: Verify both sessions exist
        Assert.NotNull(sessions);
        Assert.Equal(2, sessions.Count);
        Assert.Equal("run", sessions[0].Type);
        Assert.Equal("strength", sessions[1].Type);
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

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<WeekDto>>();
        Assert.NotNull(weeks);

        var dayPlanId = weeks[0].DayPlans[0].Id;

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

        var target = await result.Content.ReadFromJsonAsync<DailyNutritionTargetDto>();

        // Assert: Verify deterministic calculation
        // daily_food_target = 2000 + 400 - 500 = 1900
        Assert.NotNull(target);
        Assert.Equal(2000m, target!.DailyBaseEnergyKcal);
        Assert.Equal(400m, target.ActivityEnergyKcal);
        Assert.Equal(500m, target.TargetNetDeficitKcal);
        Assert.Equal(1900m, target.DailyFoodTargetKcal);
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

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<WeekDto>>();
        Assert.NotNull(weeks);

        var dayPlanId = weeks[0].DayPlans[0].Id;

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

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<WeekDto>>();
        var dayPlanId = weeks![0].DayPlans[0].Id;

        // Create activity session
        var createResult = await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayPlanId}", new
        {
            type = "run",
            intensity = "moderate",
            durationMinutes = 30,
            estimatedEnergyKcal = 300m
        });
        createResult.EnsureSuccessStatusCode();

        var sessionContent = await createResult.Content.ReadFromJsonAsync<ActivitySessionDto>();
        var sessionId = sessionContent!.Id;

        // Act: Update session
        var updateResult = await client.PutAsJsonAsync($"/api/activity-sessions/{sessionId}", new
        {
            durationMinutes = 45,
            estimatedEnergyKcal = 450m
        });
        updateResult.EnsureSuccessStatusCode();

        var updatedSession = await updateResult.Content.ReadFromJsonAsync<ActivitySessionDto>();

        // Assert: Verify update
        Assert.Equal(45, updatedSession!.DurationMinutes);
        Assert.Equal(450m, updatedSession.EstimatedEnergyKcal);
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

        var weeks = await weeksResult.Content.ReadFromJsonAsync<List<WeekDto>>();
        var dayPlanId = weeks![0].DayPlans[0].Id;

        // Create activity session
        var createResult = await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayPlanId}", new
        {
            type = "bike",
            intensity = "low",
            durationMinutes = 20,
            estimatedEnergyKcal = 150m
        });
        createResult.EnsureSuccessStatusCode();

        var sessionContent = await createResult.Content.ReadFromJsonAsync<ActivitySessionDto>();
        var sessionId = sessionContent!.Id;

        // Act: Delete session
        var deleteResult = await client.DeleteAsync($"/api/activity-sessions/{sessionId}");

        // Assert: Verify deletion
        Assert.Equal(HttpStatusCode.NoContent, deleteResult.StatusCode);

        // Verify session is gone
        var getResult = await client.GetAsync($"/api/activity-sessions/day/{dayPlanId}");
        getResult.EnsureSuccessStatusCode();

        var sessions = await getResult.Content.ReadFromJsonAsync<List<ActivitySessionDto>>();
        Assert.NotNull(sessions);
        Assert.Empty(sessions);
    }
}
