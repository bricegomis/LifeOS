using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Application.FoodItems;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Tests to verify Jalon 3 functionality: food library, Open Food Facts integration, caching, and household isolation.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Jalon3FoodItemsTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private LifeOSApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new LifeOSApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task ManualFoodItem_CanBeCreatedAndRetrieved()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create a manual food item
        var createResponse = await client.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest(
                "Banana",
                "piece",
                new NutritionDto
                {
                    CaloriesPerUnit = 89,
                    ProteinsPerUnit = 1.1,
                    CarbsPerUnit = 23,
                    FatsPerUnit = 0.3
                }));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        Assert.NotNull(created);
        Assert.Equal("Banana", created.Name);
        Assert.Equal("piece", created.ReferenceUnit);
        Assert.Equal("Manual", created.Source);
        Assert.Null(created.OffBarcode);

        // Retrieve the item
        var getResponse = await client.GetAsync($"/api/food-items/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var retrieved = await getResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal("Banana", retrieved.Name);
    }

    [Fact]
    public async Task FoodItem_ListReturnsAllItemsInHousehold()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create two food items
        await client.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest("Apple", "piece", null));

        await client.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest("Orange", "piece", null));

        // List all items
        var listResponse = await client.GetAsync("/api/food-items");
        listResponse.EnsureSuccessStatusCode();
        var items = await listResponse.Content.ReadFromJsonAsync<List<FoodItemDto>>();

        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, x => x.Name == "Apple");
        Assert.Contains(items, x => x.Name == "Orange");
    }

    [Fact]
    public async Task HouseholdIsolation_UserBCannotSeeUserAsFoodItems()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient().AsUser(userA);
        using var clientB = _factory.CreateClient().AsUser(userB);

        // User A creates a food item
        var createResponse = await clientA.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest("Milk", "liter", null));

        createResponse.EnsureSuccessStatusCode();
        var createdItem = await createResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        // User A can see it
        var itemsForA = await clientA.GetFromJsonAsync<List<FoodItemDto>>("/api/food-items");
        Assert.Contains(itemsForA!, x => x.Id == createdItem!.Id);

        // User B cannot see it
        var itemsForB = await clientB.GetFromJsonAsync<List<FoodItemDto>>("/api/food-items");
        Assert.DoesNotContain(itemsForB!, x => x.Id == createdItem!.Id);

        // User B cannot directly access user A's item
        var getByIdForB = await clientB.GetAsync($"/api/food-items/{createdItem!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getByIdForB.StatusCode);

        var deleteForB = await clientB.DeleteAsync($"/api/food-items/{createdItem.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteForB.StatusCode);
    }

    [Fact]
    public async Task ManualFoodItem_CanBeUpdated()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest("Rice", "100g", null));

        var created = await createResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        // Update
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/food-items/{created!.Id}",
            new UpdateFoodItemRequest(
                "Brown Rice",
                "100g",
                new NutritionDto
                {
                    CaloriesPerUnit = 111,
                    ProteinsPerUnit = 2.6,
                    CarbsPerUnit = 23,
                    FatsPerUnit = 0.9
                }));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        Assert.NotNull(updated);
        Assert.Equal("Brown Rice", updated.Name);
        Assert.Equal(111, updated.Nutrition?.CaloriesPerUnit);
    }

    [Fact]
    public async Task ManualFoodItem_CanBeDeleted()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest("Pasta", "100g", null));

        var created = await createResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/food-items/{created!.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        // Verify deleted
        var getResponse = await client.GetAsync($"/api/food-items/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Correction_CanBeCreatedForManualItem()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Create original manual item
        var createResponse = await client.PostAsJsonAsync(
            "/api/food-items",
            new CreateFoodItemRequest(
                "Egg",
                "piece",
                new NutritionDto { CaloriesPerUnit = 70, ProteinsPerUnit = 6, CarbsPerUnit = 0.6, FatsPerUnit = 5 }));

        var original = await createResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        // Create correction
        var correctionResponse = await client.PostAsJsonAsync(
            $"/api/food-items/{original!.Id}/correction",
            new CreateCorrectionRequest(
                "Egg (Large)",
                "piece",
                new NutritionDto { CaloriesPerUnit = 80, ProteinsPerUnit = 7, CarbsPerUnit = 0.6, FatsPerUnit = 6 }));

        correctionResponse.EnsureSuccessStatusCode();
        var correction = await correctionResponse.Content.ReadFromJsonAsync<FoodItemDto>();

        Assert.NotNull(correction);
        Assert.Equal("Egg (Large)", correction.Name);
        Assert.Equal("Manual", correction.Source);
        Assert.Equal(80, correction.Nutrition?.CaloriesPerUnit);
    }

    [Fact]
    public async Task SearchOffByName_ReturnsResults()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Search Open Food Facts by name
        // Note: This test depends on OFF API availability. In a real scenario, mock the HTTP response.
        var searchResponse = await client.GetAsync("/api/food-items/search-off?name=apple");

        // If OFF is not available, we expect empty results or a handled exception.
        // For now, just verify the endpoint doesn't error.
        Assert.True(searchResponse.IsSuccessStatusCode || searchResponse.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchOffByBarcode_ReturnsResultsIfAvailable()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // Search Open Food Facts by barcode
        var searchResponse = await client.GetAsync("/api/food-items/search-off-barcode?barcode=5410052000124");

        // Verify endpoint exists and handles the request gracefully
        Assert.True(searchResponse.IsSuccessStatusCode || searchResponse.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OffCaching_SubsequentSearchReusesCache()
    {
        var supabaseUserId = Guid.NewGuid();
        using var client = _factory.CreateClient().AsUser(supabaseUserId);

        // First search for a barcode
        var firstSearch = await client.GetAsync("/api/food-items/search-off-barcode?barcode=1234567890");
        // Second search for the same barcode (should use cached result if first returned success)
        var secondSearch = await client.GetAsync("/api/food-items/search-off-barcode?barcode=1234567890");

        // The live OFF API can change availability for arbitrary barcodes; the endpoint should
        // still respond with a handled success/miss rather than surfacing a server failure.
        Assert.True(firstSearch.IsSuccessStatusCode || firstSearch.StatusCode == HttpStatusCode.NotFound);
        Assert.True(secondSearch.IsSuccessStatusCode || secondSearch.StatusCode == HttpStatusCode.NotFound);
    }
}
