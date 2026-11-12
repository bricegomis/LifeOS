using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Food.Foods;
using LifeOS.Api.Food.Products;
using LifeOS.Api.Food.Recipes;
using LifeOS.Api.Food.Stores;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class RecipeCostTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Post<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task The_cost_is_a_min_max_range_across_stores_and_stays_in_the_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leclerc = await Post<StoreDto>(a, "/api/stores", new StoreRequest("Leclerc", "", false, false));
        var biocoop = await Post<StoreDto>(a, "/api/stores", new StoreRequest("Biocoop", "", true, true));
        var lentils = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Lentilles corail", "100 g", null));
        var curry = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Curry", "100 g", null));
        var pack = await Post<ProductDto>(a, "/api/products", new ProductRequest(lentils.Id, "Lentilles 500 g", 500, "g"));
        var loose = await Post<ProductDto>(a, "/api/products", new ProductRequest(lentils.Id, "Lentilles vrac", 1, "kg"));
        await Post<ProductDto>(a, $"/api/products/{pack.Id}/prices", new PriceRequest(leclerc.Id, 1.80m, today));
        await Post<ProductDto>(a, $"/api/products/{loose.Id}/prices", new PriceRequest(biocoop.Id, 5.90m, today));
        var recipe = await Post<RecipeDto>(a, "/api/recipes", new CreateRecipeRequest("Dhal", 4, 35));
        await Post<RecipeDto>(a, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(lentils.Id, 250, "g"));
        await Post<RecipeDto>(a, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(curry.Id, 10, "g"));

        var cost = (await a.GetFromJsonAsync<RecipeCostDto>($"/api/recipes/{recipe.Id}/cost"))!;
        Assert.Equal(0.90m, cost.MinTotal);
        Assert.Equal(1.48m, cost.MaxTotal);
        Assert.Equal(1, cost.MissingIngredients);
        Assert.False(cost.IsComplete);
        var line = cost.Lines.Single(l => l.FoodItemId == lentils.Id);
        Assert.Equal(leclerc.Id, line.Cheapest!.StoreId);
        Assert.Equal("kg", line.Dearest!.UnitLabel);

        var all = (await a.GetFromJsonAsync<List<RecipeCostDto>>("/api/recipes/costs"))!;
        Assert.Equal(recipe.Id, Assert.Single(all).RecipeId);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/recipes/{recipe.Id}/cost")).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<List<RecipeCostDto>>("/api/recipes/costs"))!);
    }
}
