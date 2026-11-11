using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Food.Foods;
using LifeOS.Api.Food.Recipes;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class RecipeIngredientsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Send<T>(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        using var response = await client.SendAsync(new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        });
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task An_ingredient_can_be_edited_and_removed_and_each_call_returns_the_recipe()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var lentils = await Send<FoodItemDto>(client, HttpMethod.Post, "/api/foods", new { name = "Lentilles", referenceUnit = "100g", nutrition = (object?)null });
        var rice = await Send<FoodItemDto>(client, HttpMethod.Post, "/api/foods", new { name = "Riz", referenceUnit = "100g", nutrition = (object?)null });
        var recipe = await Send<RecipeDto>(client, HttpMethod.Post, "/api/recipes", new CreateRecipeRequest("Dhal", 4, 35));
        await Send<RecipeDto>(client, HttpMethod.Post, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(lentils.Id, 250, "g"));
        recipe = await Send<RecipeDto>(client, HttpMethod.Post, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(rice.Id, 300, "g"));
        var lentilLine = recipe.Ingredients.Single(i => i.FoodItemId == lentils.Id);

        var edited = await Send<RecipeDto>(client, HttpMethod.Put, $"/api/recipes/{recipe.Id}/ingredients/{lentilLine.Id}",
            new UpdateRecipeIngredientRequest(0.3m, "kg"));
        var editedLine = edited.Ingredients.Single(i => i.Id == lentilLine.Id);
        Assert.Equal(0.3m, editedLine.Quantity);
        Assert.Equal("kg", editedLine.Unit);
        Assert.Equal(lentils.Id, editedLine.FoodItemId);
        Assert.Equal(2, edited.Ingredients.Count);

        using var invalid = await client.PutAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients/{lentilLine.Id}",
            new UpdateRecipeIngredientRequest(0, "kg"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var afterRemoval = await Send<RecipeDto>(client, HttpMethod.Delete, $"/api/recipes/{recipe.Id}/ingredients/{lentilLine.Id}");
        Assert.Equal(rice.Id, Assert.Single(afterRemoval.Ingredients).FoodItemId);

        var reloaded = await Send<RecipeDto>(client, HttpMethod.Get, $"/api/recipes/{recipe.Id}");
        Assert.Equal(300, Assert.Single(reloaded.Ingredients).Quantity);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/api/recipes/{recipe.Id}/ingredients/{lentilLine.Id}")).StatusCode);
    }

    [Fact]
    public async Task Another_household_can_neither_edit_nor_remove_an_ingredient()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var owner = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Send<FoodItemDto>(owner, HttpMethod.Post, "/api/foods", new { name = "Carotte", referenceUnit = "100g", nutrition = (object?)null });
        var recipe = await Send<RecipeDto>(owner, HttpMethod.Post, "/api/recipes", new CreateRecipeRequest("Gratin", 4, 45));
        recipe = await Send<RecipeDto>(owner, HttpMethod.Post, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(food.Id, 800, "g"));
        var line = Assert.Single(recipe.Ingredients);
        var path = $"/api/recipes/{recipe.Id}/ingredients/{line.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(path, new UpdateRecipeIngredientRequest(1, "g"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(path)).StatusCode);
        // A food from another household cannot become an ingredient either.
        var foreignFood = await Send<FoodItemDto>(other, HttpMethod.Post, "/api/foods", new { name = "Riz", referenceUnit = "100g", nutrition = (object?)null });
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients",
            new AddRecipeIngredientRequest(foreignFood.Id, 100, "g"))).StatusCode);

        var unchanged = await Send<RecipeDto>(owner, HttpMethod.Get, $"/api/recipes/{recipe.Id}");
        Assert.Equal(800, Assert.Single(unchanged.Ingredients).Quantity);
    }
}
