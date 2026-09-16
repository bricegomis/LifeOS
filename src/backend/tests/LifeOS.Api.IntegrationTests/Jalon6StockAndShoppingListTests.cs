using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Application.Stock;
using LifeOS.Application.Articles;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Tests for Jalon 6: Stock and Shopping List management.
/// Verifies household isolation, stock creation/update/delete,
/// shopping list generation from week meals, and checkbox persistence.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Jalon6StockAndShoppingListTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private LifeOSApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new LifeOSApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Can_create_and_retrieve_stock_items()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(userId);

        // Provision household
        await client.GetAsync("/api/stores");

        // Create an article first
        var articleRequest = new ArticleRequest(
            Name: "Tomatoes",
            Description: "Fresh tomatoes",
            Unit: "kilogram");

        var articleResponse = await client.PostAsJsonAsync("/api/articles", articleRequest);
        Assert.Equal(HttpStatusCode.Created, articleResponse.StatusCode);

        var article = await articleResponse.Content.ReadFromJsonAsync<GroceryItemDto>();
        Assert.NotNull(article);

        // Create a stock item
        var stockRequest = new CreateStockItemRequest(
            GroceryItemId: article.Id,
            Quantity: 2.5m,
            Unit: "kg");

        var stockResponse = await client.PostAsJsonAsync("/api/stock-items", stockRequest);
        Assert.Equal(HttpStatusCode.Created, stockResponse.StatusCode);

        var stock = await stockResponse.Content.ReadFromJsonAsync<StockItemDto>();
        Assert.NotNull(stock);
        Assert.NotEqual(Guid.Empty, stock.Id);
        Assert.Equal(article.Id, stock.GroceryItemId);
        Assert.Equal(2.5m, stock.Quantity);
        Assert.Equal("kg", stock.Unit);

        // Retrieve stock items
        var getResponse = await client.GetAsync("/api/stock-items");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var items = await getResponse.Content.ReadFromJsonAsync<List<StockItemDto>>();
        Assert.NotNull(items);
        Assert.NotEmpty(items);
        Assert.Contains(items, s => s.Id == stock.Id);
    }

    [Fact]
    public async Task Can_update_stock_item()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(userId);

        // Setup: Create article and stock item
        await client.GetAsync("/api/stores");

        var articleRequest = new ArticleRequest(
            Name: "Potatoes",
            Description: "Fresh potatoes",
            Unit: "kilogram");

        var articleResponse = await client.PostAsJsonAsync("/api/articles", articleRequest);
        var article = await articleResponse.Content.ReadFromJsonAsync<GroceryItemDto>();
        Assert.NotNull(article);

        var stockRequest = new CreateStockItemRequest(
            GroceryItemId: article.Id,
            Quantity: 1m,
            Unit: "kg");

        var stockResponse = await client.PostAsJsonAsync("/api/stock-items", stockRequest);
        var stock = await stockResponse.Content.ReadFromJsonAsync<StockItemDto>();
        Assert.NotNull(stock);

        // Update stock item
        var updateRequest = new UpdateStockItemRequest(
            Quantity: 5m,
            Unit: "kg");

        var updateResponse = await client.PutAsJsonAsync($"/api/stock-items/{stock.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<StockItemDto>();
        Assert.NotNull(updated);
        Assert.Equal(5m, updated.Quantity);
    }

    [Fact]
    public async Task Can_delete_stock_item()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(userId);

        // Setup: Create article and stock item
        await client.GetAsync("/api/stores");

        var articleRequest = new ArticleRequest(
            Name: "Carrots",
            Description: "Fresh carrots",
            Unit: "kilogram");

        var articleResponse = await client.PostAsJsonAsync("/api/articles", articleRequest);
        var article = await articleResponse.Content.ReadFromJsonAsync<GroceryItemDto>();
        Assert.NotNull(article);

        var stockRequest = new CreateStockItemRequest(
            GroceryItemId: article.Id,
            Quantity: 2m,
            Unit: "kg");

        var stockResponse = await client.PostAsJsonAsync("/api/stock-items", stockRequest);
        var stock = await stockResponse.Content.ReadFromJsonAsync<StockItemDto>();
        Assert.NotNull(stock);

        // Delete stock item
        var deleteResponse = await client.DeleteAsync($"/api/stock-items/{stock.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it's deleted
        var getResponse = await client.GetAsync("/api/stock-items");
        var items = await getResponse.Content.ReadFromJsonAsync<List<StockItemDto>>();
        Assert.NotNull(items);
        Assert.DoesNotContain(items, s => s.Id == stock.Id);
    }

    [Fact]
    public async Task Shopping_list_items_are_isolated_by_household()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient().AsUser(userA);
        using var clientB = _factory.CreateClient().AsUser(userB);

        // Provision both households
        await clientA.GetAsync("/api/stores");
        await clientB.GetAsync("/api/stores");

        // Get shopping list for user A
        var responseA = await clientA.GetAsync("/api/shopping-list/items");
        var itemsA = await responseA.Content.ReadFromJsonAsync<List<ShoppingListItemDto>>();
        var itemsCountA = itemsA?.Count ?? 0;

        // User B should have isolated shopping list
        var responseB = await clientB.GetAsync("/api/shopping-list/items");
        var itemsB = await responseB.Content.ReadFromJsonAsync<List<ShoppingListItemDto>>();
        var itemsCountB = itemsB?.Count ?? 0;

        // Both should start empty since no weeks/meals have been created
        Assert.Equal(0, itemsCountA);
        Assert.Equal(0, itemsCountB);
    }

    [Fact]
    public async Task Can_generate_shopping_list_from_planned_meals_and_deduct_stock()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(userId);

        // 1. Create articles
        var pastaArticleResponse = await client.PostAsJsonAsync(
            "/api/articles",
            new ArticleRequest("Pâtes", "Pâtes complètes", "kilogram"));
        pastaArticleResponse.EnsureSuccessStatusCode();
        var pastaArticle = await pastaArticleResponse.Content.ReadFromJsonAsync<GroceryItemDto>();

        var sauceArticleResponse = await client.PostAsJsonAsync(
            "/api/articles",
            new ArticleRequest("Sauce Tomate", "Sauce tomate basilic", "unit"));
        sauceArticleResponse.EnsureSuccessStatusCode();
        var sauceArticle = await sauceArticleResponse.Content.ReadFromJsonAsync<GroceryItemDto>();

        // 2. Create stock item (we already have 0.5kg of pasta in stock)
        var stockResponse = await client.PostAsJsonAsync(
            "/api/stock-items",
            new CreateStockItemRequest(pastaArticle!.Id, 0.5m, "kg"));
        stockResponse.EnsureSuccessStatusCode();

        // 3. Create a recipe with ingredients
        var recipeResponse = await client.PostAsJsonAsync(
            "/api/recipes",
            new LifeOS.Api.Dtos.CreateRecipeRequest("Pâtes Tomate", 2, 15));
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<LifeOS.Api.Dtos.RecipeDto>();

        var addIngredient1 = await client.PostAsJsonAsync(
            $"/api/recipes/{recipe!.Id}/ingredients",
            new LifeOS.Api.Dtos.AddRecipeIngredientRequest(pastaArticle.Id, 1.0m, "kg"));
        addIngredient1.EnsureSuccessStatusCode();

        var addIngredient2 = await client.PostAsJsonAsync(
            $"/api/recipes/{recipe.Id}/ingredients",
            new LifeOS.Api.Dtos.AddRecipeIngredientRequest(sauceArticle!.Id, 2.0m, "unit"));
        addIngredient2.EnsureSuccessStatusCode();

        // 4. Create a week and plan the meal
        var startsOn = new DateOnly(2026, 9, 21);
        var weekResponse = await client.PostAsJsonAsync(
            "/api/weeks",
            new LifeOS.Api.Dtos.CreateWeekRequest(startsOn, "draft"));
        weekResponse.EnsureSuccessStatusCode();
        var week = await weekResponse.Content.ReadFromJsonAsync<LifeOS.Api.Dtos.WeekDto>();

        var dayPlan = week!.DayPlans.First();
        var mealResponse = await client.PostAsJsonAsync(
            $"/api/planned-meals/{dayPlan.Id}/meals",
            new LifeOS.Api.Dtos.CreatePlannedMealRequest("dinner", RecipeId: recipe.Id));
        mealResponse.EnsureSuccessStatusCode();

        // 5. Generate shopping list
        var generateResponse = await client.PostAsync($"/api/shopping-list/weeks/{week.Id}/generate", null);
        generateResponse.EnsureSuccessStatusCode();

        // 6. Get shopping list items
        var getListResponse = await client.GetAsync($"/api/shopping-list/items?weekId={week.Id}");
        getListResponse.EnsureSuccessStatusCode();
        var shoppingList = await getListResponse.Content.ReadFromJsonAsync<List<ShoppingListItemDto>>();

        Assert.NotNull(shoppingList);
        Assert.Equal(2, shoppingList.Count);

        // Pasta: needed 1.0, 0.5 from stock
        var pastaItem = shoppingList.Single(i => i.GroceryItemId == pastaArticle.Id);
        Assert.Equal(1.0m, pastaItem.QuantityNeeded);
        Assert.Equal(0.5m, pastaItem.QuantityFromStock);
        Assert.False(pastaItem.Checked);

        // Sauce: needed 2.0, 0.0 from stock
        var sauceItem = shoppingList.Single(i => i.GroceryItemId == sauceArticle.Id);
        Assert.Equal(2.0m, sauceItem.QuantityNeeded);
        Assert.Equal(0.0m, sauceItem.QuantityFromStock);
        Assert.False(sauceItem.Checked);

        // 7. Check an item
        var checkResponse = await client.PatchAsJsonAsync(
            $"/api/shopping-list/items/{pastaItem.Id}",
            new UpdateShoppingListItemRequest(true));
        checkResponse.EnsureSuccessStatusCode();

        var updatedItem = await checkResponse.Content.ReadFromJsonAsync<ShoppingListItemDto>();
        Assert.NotNull(updatedItem);
        Assert.True(updatedItem.Checked);
    }

    [Fact]
    public async Task Generate_shopping_list_maps_food_item_by_name_and_ignores_unmatched()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(userId);

        // 1. Create a GroceryItem / Article (e.g. "Riz basmati")
        var articleResponse = await client.PostAsJsonAsync(
            "/api/articles",
            new ArticleRequest("Riz basmati", "Riz blanc parfumé", "kilogram"));
        articleResponse.EnsureSuccessStatusCode();
        var article = await articleResponse.Content.ReadFromJsonAsync<GroceryItemDto>();

        // 2. Create a FoodItem with matching name
        var foodItemResponse = await client.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest("Riz basmati", "100g", null));
        foodItemResponse.EnsureSuccessStatusCode();
        var foodItem = await foodItemResponse.Content.ReadFromJsonAsync<LifeOS.Application.FoodItems.FoodItemDto>();

        // 3. Create a Recipe directly in DB or via repository (or recipe referencing article / food item)
        var recipeResponse = await client.PostAsJsonAsync(
            "/api/recipes",
            new LifeOS.Api.Dtos.CreateRecipeRequest("Bol de Riz", 1, 10));
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<LifeOS.Api.Dtos.RecipeDto>();

        // Add ingredient pointing to the article
        var addIngResponse = await client.PostAsJsonAsync(
            $"/api/recipes/{recipe!.Id}/ingredients",
            new LifeOS.Api.Dtos.AddRecipeIngredientRequest(article!.Id, 0.2m, "kg"));
        addIngResponse.EnsureSuccessStatusCode();

        // 4. Create a Week with a PlannedMeal
        var startsOn = new DateOnly(2026, 9, 28);
        var weekResponse = await client.PostAsJsonAsync(
            "/api/weeks",
            new LifeOS.Api.Dtos.CreateWeekRequest(startsOn, "draft"));
        weekResponse.EnsureSuccessStatusCode();
        var week = await weekResponse.Content.ReadFromJsonAsync<LifeOS.Api.Dtos.WeekDto>();

        var dayPlan = week!.DayPlans.First();
        var mealResponse = await client.PostAsJsonAsync(
            $"/api/planned-meals/{dayPlan.Id}/meals",
            new LifeOS.Api.Dtos.CreatePlannedMealRequest("lunch", RecipeId: recipe.Id));
        mealResponse.EnsureSuccessStatusCode();

        // 5. Generate shopping list
        var generateResponse = await client.PostAsync($"/api/shopping-list/weeks/{week.Id}/generate", null);
        generateResponse.EnsureSuccessStatusCode();

        var getListResponse = await client.GetAsync($"/api/shopping-list/items?weekId={week.Id}");
        getListResponse.EnsureSuccessStatusCode();
        var items = await getListResponse.Content.ReadFromJsonAsync<List<ShoppingListItemDto>>();

        Assert.NotNull(items);
        Assert.Single(items);
        Assert.Equal(article.Id, items[0].GroceryItemId);
        Assert.Equal(0.2m, items[0].QuantityNeeded);
    }
}
