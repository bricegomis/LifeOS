using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Api.Dtos;
using LifeOS.Api.Endpoints;
using LifeOS.Application.FoodItems;
using System.Text.Json;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class FoodsAndProductsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Post<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Foods_are_used_without_products_and_archived_foods_cannot_join_recipes()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Riz", "100g",
            new NutritionDto { CaloriesPerUnit = 150, ProteinsPerUnit = 3, CarbsPerUnit = 30, FatsPerUnit = 1 }));
        var recipe = await Post<RecipeDto>(a, "/api/recipes", new CreateRecipeRequest("Bol", 2, 10));
        await Post<RecipeDto>(a, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(food.Id, 200, "g"));
        (await a.PutAsJsonAsync($"/api/foods/{food.Id}", new FoodRequest("Riz modifié", "100g", new NutritionDto { CaloriesPerUnit = 999 }))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/foods/{food.Id}")).StatusCode);
        (await a.DeleteAsync($"/api/foods/{food.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(food.Id, 1, "g"))).StatusCode);
    }

    [Fact]
    public async Task Several_products_link_one_food_and_cannot_be_used_as_recipe_ingredients()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Lait", "", null));
        var request = new ProductRequest(food.Id, "Lait marque", "", "L", null, null, null, null, false, false);
        var first = await Post<JsonElement>(a, "/api/products", request);
        var second = await Post<JsonElement>(a, "/api/products", request);
        Assert.NotEqual(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        var recipe = await Post<RecipeDto>(a, "/api/recipes", new CreateRecipeRequest("Recette", 2, 10));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients",
            new AddRecipeIngredientRequest(first.GetProperty("id").GetGuid(), 1, "L"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/api/products", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/products/{first.GetProperty("id").GetGuid()}")).StatusCode);
    }
}
