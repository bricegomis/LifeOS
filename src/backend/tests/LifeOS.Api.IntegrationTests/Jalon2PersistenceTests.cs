using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Dtos;
using LifeOS.Application.FoodItems;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class Jalon2PersistenceTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Recipe_crud_is_persistent_and_household_scoped()
    {
        var userA = Guid.NewGuid();
        await using (var factory = new LifeOSApiFactory(postgres.ConnectionString))
        {
            using var client = factory.CreateClient().AsUser(userA);
            using var otherHousehold = factory.CreateClient().AsUser(Guid.NewGuid());
            var foodResponse = await client.PostAsJsonAsync("/api/foods",
                new { name = "Lentilles", referenceUnit = "100g", nutrition = (object?)null });
            foodResponse.EnsureSuccessStatusCode();
            var food = (await foodResponse.Content.ReadFromJsonAsync<FoodItemDto>())!;
            var recipeResponse = await client.PostAsJsonAsync("/api/recipes",
                new CreateRecipeRequest("Dhal", 4, 35, ["plat", "vegan"], Instructions: "Cuire doucement."));
            recipeResponse.EnsureSuccessStatusCode();
            var recipe = (await recipeResponse.Content.ReadFromJsonAsync<RecipeDto>())!;

            var ingredientResponse = await client.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients",
                new AddRecipeIngredientRequest(food.Id, 300, "g"));
            ingredientResponse.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound,
                (await otherHousehold.GetAsync($"/api/recipes/{recipe.Id}")).StatusCode);

            var update = await client.PutAsJsonAsync($"/api/recipes/{recipe.Id}",
                new UpdateRecipeRequest(Servings: 6, Instructions: "Cuire doucement et servir."));
            update.EnsureSuccessStatusCode();
            var updated = (await update.Content.ReadFromJsonAsync<RecipeDto>())!;
            Assert.Equal(6, updated.Servings);
            Assert.Single(updated.Ingredients);
            Assert.Equal("Cuire doucement et servir.", updated.Instructions);
        }

        await using var restarted = new LifeOSApiFactory(postgres.ConnectionString);
        using var reader = restarted.CreateClient().AsUser(userA);
        var recipes = await reader.GetFromJsonAsync<List<RecipeDto>>("/api/recipes");
        var persisted = Assert.Single(recipes!);
        Assert.Equal("Dhal", persisted.Name);
        Assert.Equal(6, persisted.Servings);
        Assert.Equal("Cuire doucement et servir.", persisted.Instructions);
        Assert.Equal(300, Assert.Single(persisted.Ingredients).Quantity);
    }
}
