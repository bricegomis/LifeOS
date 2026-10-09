using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Contracts;
using LifeOS.Api.Dtos;
using LifeOS.Application.Articles;
using LifeOS.Application.FoodItems;
using LifeOS.Application.Households;
using LifeOS.Application.Stock;
using LifeOS.Application.Stores;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Integration tests for the balanced week plan (formerly Jalon 5 scenarios).
/// The user no longer picks a ranking objective: a single plan is computed and it always
/// combines nutritional balance, budget, diversity over the month and waste reduction.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BalancedWeekPlanTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private LifeOSApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new LifeOSApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Compute_takes_no_objective_and_returns_a_single_balanced_plan()
    {
        using var client = _factory.CreateClient().AsUser(Guid.NewGuid());
        var weekId = await CreateWeekAsync(client);

        var response = await client.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<BalancedPlanResponse>();

        Assert.NotNull(plan);
        Assert.Equal("balanced", plan.Method);
        Assert.False(plan.Applied);
        Assert.Equal(JsonValueKind.Object, plan.Explanation.ValueKind);

        var dimensions = plan.Explanation.GetProperty("dimensions").EnumerateArray().ToList();
        Assert.Equal(
            new[] { "nutrition", "cost", "diversity", "waste" },
            dimensions.Select(dimension => dimension.GetProperty("key").GetString()));

        Assert.False(string.IsNullOrWhiteSpace(plan.Explanation.GetProperty("textExplanation").GetString()));
    }

    [Fact]
    public async Task Compute_on_an_empty_week_reports_the_missing_data_instead_of_a_fake_score()
    {
        using var client = _factory.CreateClient().AsUser(Guid.NewGuid());
        var weekId = await CreateWeekAsync(client);

        var response = await client.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null);
        response.EnsureSuccessStatusCode();

        var plan = await response.Content.ReadFromJsonAsync<BalancedPlanResponse>();
        Assert.NotNull(plan);

        Assert.Equal(JsonValueKind.Null, plan.Explanation.GetProperty("overallScore").ValueKind);
        Assert.False(plan.Explanation.GetProperty("nutritionConstraintMet").GetBoolean());
        Assert.NotEmpty(plan.Explanation.GetProperty("limitations").EnumerateArray());

        foreach (var dimension in plan.Explanation.GetProperty("dimensions").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, dimension.GetProperty("score").ValueKind);
            Assert.Equal(0, dimension.GetProperty("weight").GetDouble());
        }
    }

    [Fact]
    public async Task Compute_scores_every_dimension_when_the_household_data_allows_it()
    {
        using var client = _factory.CreateClient().AsUser(Guid.NewGuid());
        var weekId = await SeedRealisticWeekAsync(client);

        var response = await client.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null);
        response.EnsureSuccessStatusCode();

        var plan = await response.Content.ReadFromJsonAsync<BalancedPlanResponse>();
        Assert.NotNull(plan);

        var explanation = plan.Explanation;
        var overall = explanation.GetProperty("overallScore");
        Assert.Equal(JsonValueKind.Number, overall.ValueKind);
        Assert.InRange(overall.GetDouble(), 0, 1);

        var dimensions = explanation.GetProperty("dimensions")
            .EnumerateArray()
            .ToDictionary(dimension => dimension.GetProperty("key").GetString()!);

        // Nutrition, budget and waste all rest on the seeded recipes, prices and stock.
        Assert.Equal(JsonValueKind.Number, dimensions["nutrition"].GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Number, dimensions["cost"].GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Number, dimensions["diversity"].GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Number, dimensions["waste"].GetProperty("score").ValueKind);

        // Weights of the scored dimensions are renormalised to 1.
        var totalWeight = dimensions.Values.Sum(dimension => dimension.GetProperty("weight").GetDouble());
        Assert.Equal(1.0, totalWeight, 3);

        // Nutrition stays the heaviest dimension of the compromise.
        var nutritionWeight = dimensions["nutrition"].GetProperty("weight").GetDouble();
        Assert.All(
            dimensions.Where(entry => entry.Key != "nutrition"),
            entry => Assert.True(entry.Value.GetProperty("weight").GetDouble() <= nutritionWeight));

        // The budget is compared to what the library actually makes reachable.
        var costMetrics = dimensions["cost"].GetProperty("metrics");
        Assert.True(costMetrics.GetProperty("estimatedCostEur").GetDouble() > 0);
        Assert.True(costMetrics.GetProperty("cheapestReachableCostEur").GetDouble() > 0);

        // Waste is a proxy and says so, so no "zero waste" promise is made.
        Assert.Contains(
            explanation.GetProperty("limitations").EnumerateArray(),
            limitation => limitation.GetString()!.Contains("gaspillage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Computed_plans_are_persisted_and_listed_most_recent_first()
    {
        using var client = _factory.CreateClient().AsUser(Guid.NewGuid());
        var weekId = await CreateWeekAsync(client);

        (await client.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null)).EnsureSuccessStatusCode();
        await Task.Delay(20);
        (await client.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null)).EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/api/weeks/{weekId}/balanced-plan");
        listResponse.EnsureSuccessStatusCode();

        var plans = await listResponse.Content.ReadFromJsonAsync<List<StoredBalancedPlanResponse>>();

        Assert.NotNull(plans);
        Assert.Equal(2, plans.Count);
        Assert.All(plans, plan => Assert.Equal("balanced", plan.Method));
        Assert.True(plans[0].CreatedAt >= plans[1].CreatedAt);
        Assert.All(plans, plan => Assert.False(string.IsNullOrWhiteSpace(plan.Explanation)));
    }

    [Fact]
    public async Task Applying_a_plan_marks_it_retained_and_releases_the_previous_one()
    {
        using var client = _factory.CreateClient().AsUser(Guid.NewGuid());
        var weekId = await CreateWeekAsync(client);

        var first = await ComputeAsync(client, weekId);
        var second = await ComputeAsync(client, weekId);

        var applyFirst = await client.PatchAsync($"/api/weeks/{weekId}/balanced-plan/{first}/apply", null);
        Assert.Equal(HttpStatusCode.NoContent, applyFirst.StatusCode);

        var applySecond = await client.PatchAsync($"/api/weeks/{weekId}/balanced-plan/{second}/apply", null);
        Assert.Equal(HttpStatusCode.NoContent, applySecond.StatusCode);

        var plans = await client.GetFromJsonAsync<List<StoredBalancedPlanResponse>>($"/api/weeks/{weekId}/balanced-plan");

        Assert.NotNull(plans);
        Assert.Single(plans, plan => plan.Applied);
        Assert.True(plans.Single(plan => plan.Id == second).Applied);
        Assert.False(plans.Single(plan => plan.Id == first).Applied);
    }

    [Fact]
    public async Task Balanced_plans_are_isolated_by_household()
    {
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        using var firstClient = _factory.CreateClient().AsUser(firstUser);
        using var secondClient = _factory.CreateClient().AsUser(secondUser);

        var weekId = await CreateWeekAsync(firstClient);
        (await firstClient.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null)).EnsureSuccessStatusCode();

        // Provision the second household before it tries to reach the first one's week.
        await secondClient.GetAsync("/api/stores");

        var readAttempt = await secondClient.GetAsync($"/api/weeks/{weekId}/balanced-plan");
        Assert.Equal(HttpStatusCode.NotFound, readAttempt.StatusCode);

        var computeAttempt = await secondClient.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null);
        Assert.Equal(HttpStatusCode.NotFound, computeAttempt.StatusCode);
    }

    [Fact]
    public async Task Compute_for_an_unknown_week_returns_not_found()
    {
        using var client = _factory.CreateClient().AsUser(Guid.NewGuid());
        await client.GetAsync("/api/stores");

        var response = await client.PostAsync($"/api/weeks/{Guid.NewGuid()}/balanced-plan/compute", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Applying_a_plan_of_another_week_is_rejected()
    {
        using var client = _factory.CreateClient().AsUser(Guid.NewGuid());

        var firstWeek = await CreateWeekAsync(client, new DateOnly(2026, 9, 7));
        var secondWeek = await CreateWeekAsync(client, new DateOnly(2026, 9, 14));
        var planOfFirstWeek = await ComputeAsync(client, firstWeek);

        var response = await client.PatchAsync(
            $"/api/weeks/{secondWeek}/balanced-plan/{planOfFirstWeek}/apply",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ setup

    private static async Task<Guid> CreateWeekAsync(HttpClient client, DateOnly? startsOn = null)
    {
        // Any authenticated call provisions the household on first use.
        await client.GetAsync("/api/stores");

        var response = await client.PostAsJsonAsync(
            "/api/weeks",
            new CreateWeekRequest(startsOn ?? new DateOnly(2026, 9, 21), "draft"));
        response.EnsureSuccessStatusCode();

        var week = await response.Content.ReadFromJsonAsync<WeekDto>();
        return week!.Id;
    }

    private static async Task<Guid> ComputeAsync(HttpClient client, Guid weekId)
    {
        var response = await client.PostAsync($"/api/weeks/{weekId}/balanced-plan/compute", null);
        response.EnsureSuccessStatusCode();

        var plan = await response.Content.ReadFromJsonAsync<BalancedPlanResponse>();
        return plan!.Id;
    }

    /// <summary>
    /// Seeds a household with nutrition targets, priced articles, food items carrying nutrition,
    /// two recipes of different cost, some stock and a week of meals, so every dimension of the
    /// compromise has real data to work with.
    /// </summary>
    private static async Task<Guid> SeedRealisticWeekAsync(HttpClient client)
    {
        await client.GetAsync("/api/stores");

        var storeResponse = await client.PostAsJsonAsync(
            "/api/stores",
            new StoreRequest("Marché", "1 rue du marché", false, true));
        storeResponse.EnsureSuccessStatusCode();
        var store = await storeResponse.Content.ReadFromJsonAsync<StoreDto>();

        var configResponse = await client.PostAsJsonAsync(
            "/api/nutrition/configuration",
            new UpsertUserConfigurationRequest(2200m, 200m, 100m, 220m, 70m));
        configResponse.EnsureSuccessStatusCode();

        var rice = await CreatePricedIngredientAsync(client, store!.Id, "Riz", 2.5m, calories: 350, proteins: 7, carbs: 77, fats: 1);
        var lentils = await CreatePricedIngredientAsync(client, store.Id, "Lentilles", 3.2m, calories: 320, proteins: 25, carbs: 50, fats: 2);
        var salmon = await CreatePricedIngredientAsync(client, store.Id, "Saumon", 22m, calories: 200, proteins: 20, carbs: 0, fats: 13);

        // Rice is shared by both recipes: a real "reused ingredient" for the waste proxy.
        var cheapRecipe = await CreateRecipeAsync(client, "Riz lentilles", [(rice, 1.2m), (lentils, 0.8m)]);
        var pricierRecipe = await CreateRecipeAsync(client, "Riz saumon", [(rice, 1.0m), (salmon, 0.6m)]);

        var stockResponse = await client.PostAsJsonAsync(
            "/api/stock-items",
            new CreateStockItemRequest(rice, 1m, "kg"));
        stockResponse.EnsureSuccessStatusCode();

        var weekResponse = await client.PostAsJsonAsync(
            "/api/weeks",
            new CreateWeekRequest(new DateOnly(2026, 9, 21), "draft"));
        weekResponse.EnsureSuccessStatusCode();
        var week = await weekResponse.Content.ReadFromJsonAsync<WeekDto>();

        var days = week!.DayPlans.OrderBy(day => day.Date).Take(3).ToList();
        var recipes = new[] { cheapRecipe, pricierRecipe, cheapRecipe };

        for (var index = 0; index < days.Count; index++)
        {
            var mealResponse = await client.PostAsJsonAsync(
                $"/api/planned-meals/{days[index].Id}/meals",
                new CreatePlannedMealRequest("dinner", RecipeId: recipes[index]));
            mealResponse.EnsureSuccessStatusCode();
        }

        return week.Id;
    }

    /// <summary>
    /// Creates one canonical product with nutrition and a purchase price.
    /// </summary>
    private static async Task<Guid> CreatePricedIngredientAsync(
        HttpClient client,
        Guid storeId,
        string name,
        decimal price,
        double calories,
        double proteins,
        double carbs,
        double fats)
    {
        var foodItemResponse = await client.PostAsJsonAsync(
            "/api/products",
            new CreateFoodItemRequest(
                name,
                "kilogram",
                new NutritionDto
                {
                    CaloriesPerUnit = calories,
                    ProteinsPerUnit = proteins,
                    CarbsPerUnit = carbs,
                    FatsPerUnit = fats,
                }) { Description = name, Unit = "kilogram" });
        foodItemResponse.EnsureSuccessStatusCode();
        var product = (await foodItemResponse.Content.ReadFromJsonAsync<FoodItemDto>())!;
        var priceResponse = await client.PostAsJsonAsync(
            $"/api/products/{product.Id}/price-entries",
            new PriceEntryRequest(storeId, price, DateTimeOffset.UtcNow));
        priceResponse.EnsureSuccessStatusCode();
        return product.Id;
    }

    private static async Task<Guid> CreateRecipeAsync(
        HttpClient client,
        string name,
        IEnumerable<(Guid GroceryItemId, decimal Quantity)> ingredients)
    {
        var recipeResponse = await client.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest(name, 2, 20));
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<RecipeDto>();

        foreach (var (groceryItemId, quantity) in ingredients)
        {
            var ingredientResponse = await client.PostAsJsonAsync(
                $"/api/recipes/{recipe!.Id}/ingredients",
                new AddRecipeIngredientRequest(groceryItemId, quantity, "kilogram"));
            ingredientResponse.EnsureSuccessStatusCode();
        }

        return recipe!.Id;
    }

    private sealed record BalancedPlanResponse(
        Guid Id,
        Guid WeekId,
        string Method,
        JsonElement Explanation,
        bool Applied,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record StoredBalancedPlanResponse(
        Guid Id,
        Guid WeekId,
        string Method,
        string Explanation,
        bool Applied,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
