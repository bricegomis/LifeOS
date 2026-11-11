using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Food.Recipes;
using LifeOS.Api.Endpoints;
using LifeOS.Application.FoodItems;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class HouseholdReviewsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Post<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Reviews_allow_zero_per_profile_on_foods_and_recipes_and_reject_removed_groups()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var otherHousehold = factory.CreateClient().AsUser(Guid.NewGuid());
        var profileId = (await Post<JsonElement>(client, "/api/households/profiles/",
            new { displayName = "Enfant", portionCoefficient = 0.5m })).GetProperty("id").GetGuid();
        var otherProfileId = (await Post<JsonElement>(client, "/api/households/profiles/",
            new { displayName = "Parent", portionCoefficient = 1m })).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/household-groups/")).StatusCode);
        var food = await Post<FoodItemDto>(client, "/api/foods",
            new { name = "Mozzarella", referenceUnit = "piece", nutrition = (object?)null });

        await Post<ReviewDto>(client, $"/api/reviews/foods/{food.Id}",
            new { memberProfileId = profileId, score = 0, comment = (string?)null });
        await Post<ReviewDto>(client, $"/api/reviews/foods/{food.Id}",
            new { memberProfileId = otherProfileId, score = (int?)null, comment = "Appréciée" });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/reviews/foods/{food.Id}",
            new { memberProfileId = profileId, score = 5, comment = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/reviews/foods/{food.Id}",
            new { memberProfileId = Guid.NewGuid(), score = 5, comment = (string?)null })).StatusCode);
        var foodReviews = await client.GetFromJsonAsync<List<ReviewDto>>($"/api/reviews/foods/{food.Id}");
        Assert.Equal(2, foodReviews!.Count);
        Assert.Contains(foodReviews, review => review.Score == 0 && review.MemberProfileId == profileId);
        Assert.Contains(foodReviews, review => review.Comment == "Appréciée" && review.MemberProfileId == otherProfileId && review.ProfileName == "Parent");
        Assert.Equal(HttpStatusCode.NotFound, (await otherHousehold.GetAsync($"/api/reviews/foods/{food.Id}")).StatusCode);

        var recipe = await Post<RecipeDto>(client, "/api/recipes",
            new { name = "Salade", servings = 2, durationMinutes = 0 });
        await Post<ReviewDto>(client, $"/api/reviews/recipes/{recipe.Id}",
            new { memberProfileId = profileId, score = 8, comment = (string?)null });
        var recipeReviews = await client.GetFromJsonAsync<List<ReviewDto>>($"/api/reviews/recipes/{recipe.Id}");
        Assert.Equal(8, Assert.Single(recipeReviews!).Score);
        Assert.Equal(HttpStatusCode.NotFound, (await otherHousehold.GetAsync($"/api/reviews/recipes/{recipe.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/households/profiles/{profileId}")).StatusCode);
    }
}
