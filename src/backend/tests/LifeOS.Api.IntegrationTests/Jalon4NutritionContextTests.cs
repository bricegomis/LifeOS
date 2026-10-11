using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Dtos;
using LifeOS.Application.Households;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class Jalon4NutritionContextTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Manual_planner_keeps_sports_and_nutrition_target_household_scoped()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var otherHousehold = factory.CreateClient().AsUser(Guid.NewGuid());

        var configResponse = await client.PostAsJsonAsync("/api/nutrition/configuration",
            new UpsertUserConfigurationRequest(2000, 500, 120, 220, 70));
        Assert.Equal(HttpStatusCode.Created, configResponse.StatusCode);
        var config = (await configResponse.Content.ReadFromJsonAsync<UserConfigurationDto>())!;
        Assert.Equal(2000, config.DailyBaseEnergyKcal);

        var weekResponse = await client.PostAsJsonAsync("/api/manual-planner/weeks",
            new ManualWeekRequest(new(2026, 10, 5)));
        weekResponse.EnsureSuccessStatusCode();
        var week = (await weekResponse.Content.ReadFromJsonAsync<ManualWeekDto>())!;
        var dayId = week.Days[0].Id;

        var sportResponse = await client.PostAsJsonAsync($"/api/activity-sessions/day/{dayId}",
            new CreateActivitySessionRequest("running", "moderate", 40, 350));
        Assert.Equal(HttpStatusCode.Created, sportResponse.StatusCode);
        var sport = (await sportResponse.Content.ReadFromJsonAsync<ActivitySessionDto>())!;

        var target = await client.GetFromJsonAsync<DailyNutritionTargetDto>(
            $"/api/nutrition/calculations/day/{dayId}");
        Assert.Equal(350, target!.ActivityEnergyKcal);
        Assert.Equal(1850, target.DailyFoodTargetKcal);

        Assert.Equal(HttpStatusCode.Forbidden, (await otherHousehold.GetAsync(
            $"/api/activity-sessions/day/{dayId}")).StatusCode);
        var update = await client.PutAsJsonAsync($"/api/activity-sessions/{sport.Id}",
            new UpdateActivitySessionRequest(null, null, null, 450));
        update.EnsureSuccessStatusCode();
        target = await client.GetFromJsonAsync<DailyNutritionTargetDto>(
            $"/api/nutrition/calculations/day/{dayId}");
        Assert.Equal(450, target!.ActivityEnergyKcal);

        Assert.Equal(HttpStatusCode.Forbidden, (await otherHousehold.DeleteAsync(
            $"/api/activity-sessions/{sport.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(
            $"/api/activity-sessions/{sport.Id}")).StatusCode);
        var sessions = await client.GetFromJsonAsync<List<ActivitySessionDto>>(
            $"/api/activity-sessions/day/{dayId}");
        Assert.Empty(sessions!);
    }
}
