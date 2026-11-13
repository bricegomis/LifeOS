using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Food.Foods;
using LifeOS.Api.Food.Products;
using LifeOS.Api.Food.Recipes;
using LifeOS.Api.Food.Stores;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class FoodsAndProductsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Send<T>(HttpClient client, HttpMethod method, string path, object? request = null)
    {
        using var message = new HttpRequestMessage(method, path) { Content = request is null ? null : JsonContent.Create(request) };
        var response = await client.SendAsync(message);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<T> Post<T>(HttpClient client, string path, object request) => Send<T>(client, HttpMethod.Post, path, request);

    [Fact]
    public async Task Foods_are_used_without_products_and_archived_foods_cannot_join_recipes()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Riz", "100 g",
            new NutritionDto { CaloriesPerUnit = 150, ProteinsPerUnit = 3, CarbsPerUnit = 30, FatsPerUnit = 1, FibersPerUnit = 1.4 }, Family: "grain"));
        Assert.Equal(1.4, food.Nutrition!.FibersPerUnit);
        var recipe = await Post<RecipeDto>(a, "/api/recipes", new CreateRecipeRequest("Bol", 2, 10));
        await Post<RecipeDto>(a, $"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(food.Id, 200, "g"));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/foods", new FoodRequest("Riz", "100g", null, Family: "grain"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/foods/{food.Id}")).StatusCode);
        (await a.DeleteAsync($"/api/foods/{food.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients", new AddRecipeIngredientRequest(food.Id, 1, "g"))).StatusCode);
    }

    [Fact]
    public async Task A_food_needs_a_family_and_keeps_its_classification()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/foods", new { name = "Tofu", referenceUnit = "100 g" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/foods", new FoodRequest("Tofu", "100 g", null, Family: "soja"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/foods",
            new FoodRequest("Tofu", "100 g", null, Family: "legume", SeasonMonths: [13]))).StatusCode);

        var tofu = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Tofu", "100 g", null, Family: "legume",
            Roles: ["protein"], Conservation: "fresh", Tags: ["wok", "WOK", "apéro"]));
        Assert.Equal("legume", tofu.Family);
        Assert.Equal(["protein"], tofu.Roles);
        Assert.Equal(["wok", "apéro"], tofu.Tags);
        Assert.Empty(tofu.SeasonMonths);

        var updated = await Send<FoodItemDto>(a, HttpMethod.Put, $"/api/foods/{tofu.Id}", new FoodRequest("Tofu fumé", "100 g", null,
            Family: "legume", Roles: ["protein", "fat"], Conservation: "frozen", SeasonMonths: [12, 1]));
        var reloaded = (await a.GetFromJsonAsync<FoodItemDto>($"/api/foods/{updated.Id}"))!;
        Assert.Equal(["protein", "fat"], reloaded.Roles);
        Assert.Equal("frozen", reloaded.Conservation);
        Assert.Equal([1, 12], reloaded.SeasonMonths);
        Assert.Empty(reloaded.Tags);
    }

    [Fact]
    public async Task A_product_needs_its_package_contents_and_a_food_of_the_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Lait de coco", "100 ml", null, Family: "dairy"));
        var request = new ProductRequest(food.Id, "Lait de coco 400 ml", 400, "ml", "Marque");
        var first = await Post<ProductDto>(a, "/api/products", request);
        var second = await Post<ProductDto>(a, "/api/products", request);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/products", request with { PackageQuantity = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/products", request with { PackageUnit = "boîte" })).StatusCode);

        var recipe = await Post<RecipeDto>(a, "/api/recipes", new CreateRecipeRequest("Recette", 2, 10));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients",
            new AddRecipeIngredientRequest(first.Id, 1, "ml"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/api/products", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/products/{first.Id}")).StatusCode);
        Assert.Equal(2, (await a.GetFromJsonAsync<List<ProductDto>>($"/api/products?foodId={food.Id}"))!.Count);
        Assert.Empty((await a.GetFromJsonAsync<List<ProductDto>>($"/api/products?foodId={Guid.NewGuid()}"))!);
    }

    [Fact]
    public async Task Prices_come_down_to_a_unit_price_keep_their_package_and_only_use_the_household_stores()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Emmental râpé", "100 g", null, Family: "dairy"));
        var leclerc = await Post<StoreDto>(a, "/api/stores", new StoreRequest("Leclerc", "", false, false));
        var foreignStore = await Post<StoreDto>(b, "/api/stores", new StoreRequest("Autre", "", false, false));
        var product = await Post<ProductDto>(a, "/api/products", new ProductRequest(food.Id, "Emmental 400 g", 400, "g"));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        product = await Post<ProductDto>(a, $"/api/products/{product.Id}/prices", new PriceRequest(leclerc.Id, 5.95m, today.AddDays(-7)));
        product = await Post<ProductDto>(a, $"/api/products/{product.Id}/prices", new PriceRequest(leclerc.Id, 6.10m, today));
        var latest = Assert.Single(product.LatestPrices);
        Assert.Equal(6.10m, latest.Price);
        Assert.Equal(15.25m, latest.UnitPrice);
        Assert.Equal("kg", latest.UnitLabel);
        Assert.Equal(2, product.Prices.Count);

        product = await Send<ProductDto>(a, HttpMethod.Put, $"/api/products/{product.Id}", new ProductRequest(food.Id, "Emmental 150 g", 150, "g"));
        Assert.All(product.Prices, p => Assert.Equal(400, p.PackageQuantity));

        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/products/{product.Id}/prices",
            new PriceRequest(foreignStore.Id, 1, today))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/products/{product.Id}/prices",
            new PriceRequest(foreignStore.Id, 1, today))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.DeleteAsync($"/api/stores/{leclerc.Id}")).StatusCode);

        product = await Send<ProductDto>(a, HttpMethod.Delete, $"/api/products/{product.Id}/prices/{latest.Id}");
        Assert.Equal(5.95m, Assert.Single(product.LatestPrices).Price);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/products/{product.Id}/prices/{product.Prices[0].Id}")).StatusCode);
    }
}
