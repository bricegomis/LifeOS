using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Food.Foods;
using LifeOS.Api.Food.Stores;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class FoodCompletionTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Manual_partial_foods_can_be_completed_and_archived_without_losing_nutrition()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/foods", new FoodRequest("Aliment", "", new NutritionDto { CaloriesPerUnit = 0 }, Family: "other"));
        response.EnsureSuccessStatusCode();
        var food = (await response.Content.ReadFromJsonAsync<FoodItemDto>())!;
        Assert.Equal(7, food.MissingFields.Count);
        response = await client.PutAsJsonAsync($"/api/foods/{food.Id}", new FoodRequest("Aliment", "100 g", new NutritionDto
        { CaloriesPerUnit = 0, ProteinsPerUnit = 0, CarbsPerUnit = 0, FatsPerUnit = 0, FibersPerUnit = 0 }, "low", 1, Family: "other"));
        response.EnsureSuccessStatusCode();
        food = (await response.Content.ReadFromJsonAsync<FoodItemDto>())!;
        Assert.Empty(food.MissingFields);
        Assert.Null(food.DigestiveNotes);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/foods/{food.Id}", new FoodRequest("Aliment", "", null, "unknown", 5, Family: "other"))).StatusCode);
        (await client.DeleteAsync($"/api/foods/{food.Id}")).EnsureSuccessStatusCode();
        food = (await client.GetFromJsonAsync<FoodItemDto>($"/api/foods/{food.Id}"))!;
        Assert.True(food.IsArchived); Assert.Equal(0, food.Nutrition!.CaloriesPerUnit);
    }
}
