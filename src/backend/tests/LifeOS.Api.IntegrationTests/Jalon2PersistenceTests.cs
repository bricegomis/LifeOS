using System.Net.Http.Json;
using LifeOS.Api.Dtos;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Tests to verify persistence and household isolation for Jalon 2 entities:
/// recipes, composed meals, and week planning.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Jalon2PersistenceTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Recipe_CanBeCreatedAndRetrieved()
    {
        var supabaseUserId = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(supabaseUserId);

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest(
                "Tomato Soup",
                4,
                30,
                ["vegetarian", "express"]));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeDto>();

        Assert.NotNull(created);
        Assert.Equal("Tomato Soup", created.Name);
        Assert.Equal(4, created.Servings);

        // Retrieve
        var getResponse = await client.GetAsync($"/api/recipes/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var retrieved = await getResponse.Content.ReadFromJsonAsync<RecipeDto>();

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
    }

    [Fact]
    public async Task ComposedMeal_CanBeCreatedAndRetrieved()
    {
        var supabaseUserId = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(supabaseUserId);

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/composed-meals",
            new CreateComposedMealRequest("Sunday Dinner"));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ComposedMealDto>();

        Assert.NotNull(created);
        Assert.Equal("Sunday Dinner", created.Name);

        // Retrieve
        var getResponse = await client.GetAsync($"/api/composed-meals/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var retrieved = await getResponse.Content.ReadFromJsonAsync<ComposedMealDto>();

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
    }

    [Fact]
    public async Task Week_CanBeCreatedWithDayPlans()
    {
        var supabaseUserId = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(supabaseUserId);

        // Create week
        var startsOn = new DateOnly(2026, 9, 21);
        var createResponse = await client.PostAsJsonAsync(
            "/api/weeks",
            new CreateWeekRequest(startsOn, "draft"));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<WeekDto>();

        Assert.NotNull(created);
        Assert.Equal(startsOn, created.StartsOn);
        Assert.Equal(7, created.DayPlans.Count);

        // Retrieve
        var getResponse = await client.GetAsync($"/api/weeks/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var retrieved = await getResponse.Content.ReadFromJsonAsync<WeekDto>();

        Assert.NotNull(retrieved);
        Assert.Equal(7, retrieved.DayPlans.Count);
    }

    [Fact]
    public async Task PlannedMeal_CanBeCreatedAndRetrieved()
    {
        var supabaseUserId = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(supabaseUserId);

        // Create recipe first
        var recipeResponse = await client.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest("Quick Pasta", 2, 15));
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<RecipeDto>();

        // Create week
        var startsOn = new DateOnly(2026, 9, 21);
        var weekResponse = await client.PostAsJsonAsync(
            "/api/weeks",
            new CreateWeekRequest(startsOn));
        weekResponse.EnsureSuccessStatusCode();
        var week = await weekResponse.Content.ReadFromJsonAsync<WeekDto>();

        var dayPlan = week!.DayPlans.First();

        // Create planned meal
        var mealResponse = await client.PostAsJsonAsync(
            $"/api/planned-meals/{dayPlan.Id}/meals",
            new CreatePlannedMealRequest("lunch", RecipeId: recipe!.Id));

        mealResponse.EnsureSuccessStatusCode();
        var plannedMeal = await mealResponse.Content.ReadFromJsonAsync<PlannedMealDto>();

        Assert.NotNull(plannedMeal);
        Assert.Equal("lunch", plannedMeal.MealType);
        Assert.Equal(recipe.Id, plannedMeal.RecipeId);

        // Retrieve
        var getResponse = await client.GetAsync($"/api/planned-meals/{plannedMeal.Id}");
        getResponse.EnsureSuccessStatusCode();
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PlannedMealDto>();

        Assert.NotNull(retrieved);
        Assert.Equal(plannedMeal.Id, retrieved.Id);
    }

    [Fact]
    public async Task Recipes_AreIsolatedByHousehold()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);

        // User 1 creates a recipe
        using var client1 = factory.CreateClient().AsUser(user1);
        var response1 = await client1.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest("User 1 Recipe", 2, 20));
        response1.EnsureSuccessStatusCode();

        // User 2 creates a recipe
        using var client2 = factory.CreateClient().AsUser(user2);
        var response2 = await client2.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest("User 2 Recipe", 2, 20));
        response2.EnsureSuccessStatusCode();

        // User 1 should only see their recipe
        var recipes1 = await client1.GetFromJsonAsync<List<RecipeDto>>("/api/recipes");
        Assert.Single(recipes1!);
        Assert.Equal("User 1 Recipe", recipes1[0].Name);

        // User 2 should only see their recipe
        var recipes2 = await client2.GetFromJsonAsync<List<RecipeDto>>("/api/recipes");
        Assert.Single(recipes2!);
        Assert.Equal("User 2 Recipe", recipes2[0].Name);
    }

    [Fact]
    public async Task ComposedMeals_AreIsolatedByHousehold()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);

        // User 1 creates a composed meal
        using var client1 = factory.CreateClient().AsUser(user1);
        var response1 = await client1.PostAsJsonAsync(
            "/api/composed-meals",
            new CreateComposedMealRequest("User 1 Meal"));
        response1.EnsureSuccessStatusCode();

        // User 2 creates a composed meal
        using var client2 = factory.CreateClient().AsUser(user2);
        var response2 = await client2.PostAsJsonAsync(
            "/api/composed-meals",
            new CreateComposedMealRequest("User 2 Meal"));
        response2.EnsureSuccessStatusCode();

        // User 1 should only see their meal
        var meals1 = await client1.GetFromJsonAsync<List<ComposedMealDto>>("/api/composed-meals");
        Assert.Single(meals1!);
        Assert.Equal("User 1 Meal", meals1[0].Name);

        // User 2 should only see their meal
        var meals2 = await client2.GetFromJsonAsync<List<ComposedMealDto>>("/api/composed-meals");
        Assert.Single(meals2!);
        Assert.Equal("User 2 Meal", meals2[0].Name);
    }

    [Fact]
    public async Task Weeks_AreIsolatedByHousehold()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);

        var startsOn = new DateOnly(2026, 9, 21);

        // User 1 creates a week
        using var client1 = factory.CreateClient().AsUser(user1);
        var response1 = await client1.PostAsJsonAsync(
            "/api/weeks",
            new CreateWeekRequest(startsOn, "draft"));
        response1.EnsureSuccessStatusCode();

        // User 2 creates a week for the same date
        using var client2 = factory.CreateClient().AsUser(user2);
        var response2 = await client2.PostAsJsonAsync(
            "/api/weeks",
            new CreateWeekRequest(startsOn, "draft"));
        response2.EnsureSuccessStatusCode();

        // User 1 should only see their week
        var weeks1 = await client1.GetFromJsonAsync<List<WeekDto>>("/api/weeks");
        Assert.Single(weeks1!);

        // User 2 should only see their week
        var weeks2 = await client2.GetFromJsonAsync<List<WeekDto>>("/api/weeks");
        Assert.Single(weeks2!);
    }
}
