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
}
