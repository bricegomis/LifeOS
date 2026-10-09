using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Api.Dtos;
using LifeOS.Api.Endpoints;
using LifeOS.Application.Articles;
using LifeOS.Application.FoodItems;
using LifeOS.Application.Stores;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class UnifiedProductsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Post<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task One_product_is_created_edited_priced_used_and_archived_without_altering_snapshots()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var product = await Post<FoodItemDto>(a, "/api/products", new CreateFoodItemRequest("Riz", "100g",
            new NutritionDto { CaloriesPerUnit = 150, ProteinsPerUnit = 3, CarbsPerUnit = 30, FatsPerUnit = 1 })
            { Description = "Un seul produit", Unit = "kilogram" });
        Assert.Equal(product.Id, product.ArticleId);
        Assert.Equal("kilogram", product.Unit);
        Assert.Equal("100g", product.ReferenceUnit);
        Assert.True(product.PurchaseUnitConfirmed);
        await using var db = factory.CreateDbContext();
        Assert.Single(await db.FoodItems.Where(p => p.HouseholdId == product.HouseholdId).ToListAsync());
        Assert.Single((await a.GetFromJsonAsync<List<GroceryItemDto>>("/api/articles"))!);
        var store = await Post<StoreDto>(a, "/api/stores", new StoreRequest("Marché", "", false, true));
        var foreignStore = await Post<StoreDto>(b, "/api/stores", new StoreRequest("Autre foyer", "", false, false));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/products/{product.Id}/price-entries",
            new PriceEntryRequest(foreignStore.Id, 2.5m, DateTimeOffset.UtcNow))).StatusCode);
        await Post<GroceryItemDto>(a, $"/api/products/{product.Id}/price-entries", new PriceEntryRequest(store.Id, 2.5m, DateTimeOffset.UtcNow));
        var recipe = await Post<RecipeDto>(a, "/api/recipes", new CreateRecipeRequest("Bol", 2, 10));
        await Post<RecipeDto>(a, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(product.Id, 200, "g"));
        var week = await Post<ManualWeekDto>(a, "/api/manual-planner/weeks", new ManualWeekRequest(new(2026, 10, 5)));
        var meal = await Post<ManualMealDto>(a, "/api/manual-planner/meals", new ManualMealRequest(week.Days[0].Id, 720, 750, 1, 0, recipe.Id));
        Assert.Equal(150, meal.Nutrition.Calories);
        var incompatible = await Post<ManualMealDto>(a, "/api/manual-planner/meals",
            new ManualMealRequest(week.Days[1].Id, 720, 750, 1, 0, Lines: [new(product.Id, 1, "piece")]));
        Assert.Null(incompatible.Nutrition.Calories);
        Assert.False(incompatible.Nutrition.IsComplete);
        var edit = new UpdateFoodItemRequest("Riz modifié", "100g", new NutritionDto { CaloriesPerUnit = 999 })
            { Description = "Description modifiée", Unit = "kilogram" };
        (await a.PutAsJsonAsync($"/api/products/{product.Id}", edit)).EnsureSuccessStatusCode();
        var updated = (await a.GetFromJsonAsync<FoodItemDto>($"/api/products/{product.Id}"))!;
        Assert.Equal("Description modifiée", updated.Description);
        Assert.Equal(999, updated.Nutrition!.CaloriesPerUnit);
        Assert.Equal(2.5m, Assert.Single(updated.PriceHistory).Price);
        edit.Unit = "liter";
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync($"/api/products/{product.Id}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/products/{product.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/products/{product.Id}/usage")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/products/{product.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await a.PutAsJsonAsync($"/api/food-items/{product.Id}/article", new { articleId = Guid.NewGuid() })).StatusCode);
        var usage = (await a.GetFromJsonAsync<FoodItemsEndpoints.ProductUsageDto>($"/api/products/{product.Id}/usage"))!;
        Assert.Equal(recipe.Id, Assert.Single(usage.Recipes).Id);
        Assert.Equal(2, usage.MealOccurrences);
        (await a.DeleteAsync($"/api/products/{product.Id}")).EnsureSuccessStatusCode();
        updated = (await a.GetFromJsonAsync<FoodItemDto>($"/api/products/{product.Id}"))!;
        Assert.True(updated.IsArchived);
        Assert.Single(updated.PriceHistory);
        var persisted = (await a.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{week.Id}"))!;
        Assert.Equal(150, persisted.Days[0].Meals[0].Nutrition.Calories);
        Assert.Equal("Riz", persisted.Days[0].Meals[0].Lines[0].Name);
    }

    [Fact]
    public async Task Non_food_products_same_names_and_corrections_remain_distinct()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var request = new CreateFoodItemRequest("Savon", "", null) { Unit = "unit", Description = "Sans nutrition" };
        var soap = await Post<FoodItemDto>(client, "/api/products", request);
        var other = await Post<FoodItemDto>(client, "/api/products", request);
        Assert.NotEqual(soap.Id, other.Id);
        Assert.Null(soap.Nutrition);
        Assert.Equal("", soap.ReferenceUnit);
        var corrected = await Post<FoodItemDto>(client, $"/api/products/{soap.Id}/correction",
            new CreateCorrectionRequest("Savon corrigé", "", null) { Description = "Correction", Unit = "unit" });
        Assert.Equal(soap.Id, corrected.IsCorrectionOf);
        Assert.Empty(corrected.PriceHistory);
        Assert.Equal(3, (await client.GetFromJsonAsync<List<FoodItemDto>>("/api/products"))!.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/products",
            new CreateFoodItemRequest("Incomplet", "", new NutritionDto { CaloriesPerUnit = 10 }) { Unit = "unit" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/products",
            new CreateFoodItemRequest("Sans unité d'achat", "", null))).StatusCode);
    }
}
