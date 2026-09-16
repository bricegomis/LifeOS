using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Application.Articles;
using LifeOS.Application.Stores;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Proves the core milestone 1 exit criterion: household isolation is strict. Two distinct
/// Supabase users are auto-provisioned into two distinct households (see
/// <c>ResolveHouseholdForUserQuery</c>) and must never see each other's articles or stores.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class HouseholdIsolationTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private LifeOSApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new LifeOSApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Two_users_are_provisioned_into_two_distinct_households()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient().AsUser(userA);
        using var clientB = _factory.CreateClient().AsUser(userB);

        // Provisioning happens lazily on first authenticated request to any household-scoped endpoint.
        await clientA.GetAsync("/api/stores");
        await clientB.GetAsync("/api/stores");

        await using var dbContext = _factory.CreateDbContext();
        var householdIdForA = dbContext.HouseholdMembers.Single(m => m.SupabaseUserId == userA).HouseholdId;
        var householdIdForB = dbContext.HouseholdMembers.Single(m => m.SupabaseUserId == userB).HouseholdId;

        Assert.NotEqual(householdIdForA, householdIdForB);
    }

    [Fact]
    public async Task A_household_never_sees_another_households_articles()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient().AsUser(userA);
        using var clientB = _factory.CreateClient().AsUser(userB);

        var createResponse = await clientA.PostAsJsonAsync(
            "/api/articles",
            new ArticleRequest("Lait", "Lait demi-écrémé", "liter"));

        createResponse.EnsureSuccessStatusCode();
        var createdArticle = await createResponse.Content.ReadFromJsonAsync<GroceryItemDto>();

        // Household A can read its own article back.
        var articlesForA = await clientA.GetFromJsonAsync<List<GroceryItemDto>>("/api/articles");
        Assert.Contains(articlesForA!, a => a.Id == createdArticle!.Id);

        // Household B must see nothing.
        var articlesForB = await clientB.GetFromJsonAsync<List<GroceryItemDto>>("/api/articles");
        Assert.Empty(articlesForB!);

        // Household B cannot fetch/update/delete household A's article directly by id either.
        var getByIdForB = await clientB.PutAsJsonAsync(
            $"/api/articles/{createdArticle!.Id}",
            new ArticleRequest("Hack", "Hack", "unit"));
        Assert.Equal(HttpStatusCode.NotFound, getByIdForB.StatusCode);

        var deleteForB = await clientB.DeleteAsync($"/api/articles/{createdArticle.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteForB.StatusCode);
    }

    [Fact]
    public async Task A_household_never_sees_another_households_stores()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient().AsUser(userA);
        using var clientB = _factory.CreateClient().AsUser(userB);

        // Provision both households first.
        await clientA.GetAsync("/api/stores");
        await clientB.GetAsync("/api/stores");

        await using (var dbContext = _factory.CreateDbContext())
        {
            var householdIdForA = dbContext.HouseholdMembers.Single(m => m.SupabaseUserId == userA).HouseholdId;

            dbContext.Stores.Add(LifeOS.Domain.Stores.Store.Create(householdIdForA, "Marché de A", "1 rue de A", true, true));
            await dbContext.SaveChangesAsync();
        }

        var storesForA = await clientA.GetFromJsonAsync<List<StoreDto>>("/api/stores");
        var storesForB = await clientB.GetFromJsonAsync<List<StoreDto>>("/api/stores");

        Assert.Contains(storesForA!, s => s.Name == "Marché de A");
        Assert.DoesNotContain(storesForB!, s => s.Name == "Marché de A");
    }

    [Fact]
    public async Task DayPlans_and_PlannedMeals_are_strictly_isolated_across_households()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient().AsUser(userA);
        using var clientB = _factory.CreateClient().AsUser(userB);

        // User A creates a recipe
        var recipeResponse = await clientA.PostAsJsonAsync(
            "/api/recipes",
            new LifeOS.Api.Dtos.CreateRecipeRequest("Pasta", 2, 15));
        recipeResponse.EnsureSuccessStatusCode();
        var recipeA = await recipeResponse.Content.ReadFromJsonAsync<LifeOS.Api.Dtos.RecipeDto>();

        // User A creates a week (which creates 7 day plans)
        var startsOn = new DateOnly(2026, 9, 21);
        var weekResponse = await clientA.PostAsJsonAsync(
            "/api/weeks",
            new LifeOS.Api.Dtos.CreateWeekRequest(startsOn, "draft"));
        weekResponse.EnsureSuccessStatusCode();
        var weekA = await weekResponse.Content.ReadFromJsonAsync<LifeOS.Api.Dtos.WeekDto>();
        Assert.NotNull(weekA);
        Assert.NotEmpty(weekA.DayPlans);

        var dayPlanA = weekA.DayPlans.First();

        // User A creates a planned meal on DayPlan A
        var mealResponse = await clientA.PostAsJsonAsync(
            $"/api/planned-meals/{dayPlanA.Id}/meals",
            new LifeOS.Api.Dtos.CreatePlannedMealRequest("dinner", RecipeId: recipeA!.Id));
        mealResponse.EnsureSuccessStatusCode();
        var mealA = await mealResponse.Content.ReadFromJsonAsync<LifeOS.Api.Dtos.PlannedMealDto>();
        Assert.NotNull(mealA);

        // User B cannot read User A's DayPlan
        var getDayPlanForB = await clientB.GetAsync($"/api/day-plans/{dayPlanA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDayPlanForB.StatusCode);

        // User B cannot create a DayPlan on User A's Week
        var createDayPlanForB = await clientB.PostAsJsonAsync(
            $"/api/day-plans/{weekA.Id}/days",
            new LifeOS.Api.Dtos.CreateDayPlanRequest(startsOn.AddDays(10), "office", true));
        Assert.Equal(HttpStatusCode.NotFound, createDayPlanForB.StatusCode);

        // User B cannot update User A's DayPlan
        var updateDayPlanForB = await clientB.PutAsJsonAsync(
            $"/api/day-plans/{dayPlanA.Id}",
            new LifeOS.Api.Dtos.UpdateDayPlanRequest("office", true));
        Assert.Equal(HttpStatusCode.NotFound, updateDayPlanForB.StatusCode);

        // User B cannot read User A's PlannedMeal
        var getMealForB = await clientB.GetAsync($"/api/planned-meals/{mealA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getMealForB.StatusCode);

        // User B cannot create a PlannedMeal on User A's DayPlan
        var createMealForB = await clientB.PostAsJsonAsync(
            $"/api/planned-meals/{dayPlanA.Id}/meals",
            new LifeOS.Api.Dtos.CreatePlannedMealRequest("lunch", RecipeId: recipeA.Id));
        Assert.Equal(HttpStatusCode.NotFound, createMealForB.StatusCode);

        // User B cannot update status of User A's PlannedMeal
        var updateMealStatusForB = await clientB.PutAsJsonAsync(
            $"/api/planned-meals/{mealA.Id}/status",
            new LifeOS.Api.Dtos.UpdatePlannedMealStatusRequest("consumed"));
        Assert.Equal(HttpStatusCode.NotFound, updateMealStatusForB.StatusCode);

        // User B cannot replace User A's PlannedMeal
        var replaceMealForB = await clientB.PutAsJsonAsync(
            $"/api/planned-meals/{mealA.Id}/replace",
            new LifeOS.Api.Dtos.ReplacePlannedMealRequest(RecipeId: recipeA.Id));
        Assert.Equal(HttpStatusCode.NotFound, replaceMealForB.StatusCode);

        // User B cannot delete User A's PlannedMeal
        var deleteMealForB = await clientB.DeleteAsync($"/api/planned-meals/{mealA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteMealForB.StatusCode);

        // User B cannot delete User A's DayPlan
        var deleteDayPlanForB = await clientB.DeleteAsync($"/api/day-plans/{dayPlanA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteDayPlanForB.StatusCode);

        // User A CAN delete their own PlannedMeal and DayPlan
        var deleteMealForA = await clientA.DeleteAsync($"/api/planned-meals/{mealA.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteMealForA.StatusCode);

        var deleteDayPlanForA = await clientA.DeleteAsync($"/api/day-plans/{dayPlanA.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteDayPlanForA.StatusCode);
    }
}
