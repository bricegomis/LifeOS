using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Api.Dtos;
using LifeOS.Application.Articles;
using LifeOS.Application.Stock;
using LifeOS.Domain.Stock;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class Jalon6StockAndShoppingListTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Stock_create_update_delete_uses_the_product_identity()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/articles", new ArticleRequest("Tomates", "", "kilogram"));
        response.EnsureSuccessStatusCode();
        var product = (await response.Content.ReadFromJsonAsync<GroceryItemDto>())!;
        response = await client.PostAsJsonAsync("/api/stock-items", new CreateStockItemRequest(product.Id, 2.5m, "kg"));
        response.EnsureSuccessStatusCode();
        var stock = (await response.Content.ReadFromJsonAsync<StockItemDto>())!;
        Assert.Equal(product.Id, stock.GroceryItemId);
        Assert.Equal(2.5m, stock.Quantity);
        Assert.Equal("kg", stock.Unit);
        Assert.Contains((await client.GetFromJsonAsync<List<StockItemDto>>("/api/stock-items"))!, s => s.Id == stock.Id);
        response = await client.PutAsJsonAsync($"/api/stock-items/{stock.Id}", new UpdateStockItemRequest(5, "kg"));
        response.EnsureSuccessStatusCode();
        Assert.Equal(5, (await response.Content.ReadFromJsonAsync<StockItemDto>())!.Quantity);
        (await client.DeleteAsync($"/api/stock-items/{stock.Id}")).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<StockItemDto>>("/api/stock-items"))!, s => s.Id == stock.Id);
    }

    [Fact]
    public async Task Stock_and_shopping_are_isolated_by_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var response = await a.PostAsJsonAsync("/api/articles", new ArticleRequest("Riz", "", "kilogram"));
        response.EnsureSuccessStatusCode();
        var product = (await response.Content.ReadFromJsonAsync<GroceryItemDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/api/stock-items", new CreateStockItemRequest(product.Id, 1, "kg"))).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<List<ShoppingListItemDto>>("/api/shopping-list/items"))!);
        Assert.Empty((await b.GetFromJsonAsync<List<StockItemDto>>("/api/stock-items"))!);
    }

    [Fact]
    public async Task Generation_is_disabled_even_for_legacy_weeks_and_preserves_checked_history()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/articles", new ArticleRequest("Pâtes", "", "kilogram"));
        response.EnsureSuccessStatusCode();
        var product = (await response.Content.ReadFromJsonAsync<GroceryItemDto>())!;
        response = await client.PostAsJsonAsync("/api/weeks", new CreateWeekRequest(new(2026, 9, 21), "draft"));
        response.EnsureSuccessStatusCode();
        var week = (await response.Content.ReadFromJsonAsync<WeekDto>())!;
        await using var db = factory.CreateDbContext();
        var household = await db.FoodItems.Where(p => p.Id == product.Id).Select(p => p.HouseholdId).SingleAsync();
        var history = ShoppingListItem.Create(household, week.Id, product.Id, 1, .5m);
        history.UpdateChecked(true);
        db.ShoppingListItems.Add(history);
        await db.SaveChangesAsync();
        response = await client.PostAsync($"/api/shopping-list/weeks/{week.Id}/generate", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("hors MVP manuel", await response.Content.ReadAsStringAsync());
        var items = (await client.GetFromJsonAsync<List<ShoppingListItemDto>>($"/api/shopping-list/items?weekId={week.Id}"))!;
        var item = Assert.Single(items);
        Assert.Equal(history.Id, item.Id);
        Assert.True(item.Checked);
        Assert.Equal(1, item.QuantityNeeded);
        Assert.Equal(.5m, item.QuantityFromStock);
        response = await client.PatchAsJsonAsync($"/api/shopping-list/items/{item.Id}", new UpdateShoppingListItemRequest(false));
        response.EnsureSuccessStatusCode();
        Assert.False((await response.Content.ReadFromJsonAsync<ShoppingListItemDto>())!.Checked);
    }
}
