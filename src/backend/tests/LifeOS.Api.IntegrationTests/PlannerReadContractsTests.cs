using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Authentication;
using LifeOS.Api.Food.Foods;
using LifeOS.Api.Food.Products;
using LifeOS.Api.Food.Recipes;
using LifeOS.Api.Food.Stores;
using LifeOS.Api.Health;
using LifeOS.Api.Households;
using LifeOS.Application.Food.Contracts;
using LifeOS.Application.Health.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0009: the Food and Health read contracts published for the planner read one household (and one account for health).</summary>
[Collection(PostgresCollection.Name)]
public sealed class PlannerReadContractsTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Post<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task The_food_contract_reads_the_recipes_of_one_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var store = await Post<StoreDto>(a, "/api/stores", new StoreRequest("Marché", "", false, false));
        var lentils = await Post<FoodItemDto>(a, "/api/foods", new FoodRequest("Lentilles corail", "100 g", null, Family: "legume"));
        var pack = await Post<ProductDto>(a, "/api/products", new ProductRequest(lentils.Id, "Lentilles 500 g", 500, "g"));
        await Post<ProductDto>(a, $"/api/products/{pack.Id}/prices", new PriceRequest(store.Id, 2m, DateOnly.FromDateTime(DateTime.UtcNow)));
        var dhal = await Post<RecipeDto>(a, "/api/recipes", new CreateRecipeRequest("Dhal", 2, 35));
        await Post<RecipeDto>(a, $"/api/recipes/{dhal.Id}/ingredients", new AddRecipeIngredientRequest(lentils.Id, 250, "g"));
        var other = await Post<RecipeDto>(b, "/api/recipes", new CreateRecipeRequest("Autre foyer", 1, 0));

        Guid household;
        await using (var db = factory.CreateDbContext())
            household = await db.Recipes.Where(r => r.Id == dhal.Id).Select(r => r.HouseholdId).SingleAsync();
        using var scope = factory.Services.CreateScope();
        var contract = scope.ServiceProvider.GetRequiredService<FoodReadContract>();

        var recipe = Assert.Single(await contract.RecipesAsync(household, null, CancellationToken.None));
        Assert.Equal((dhal.Id, "Lentilles corail", 250m), (recipe.Id, recipe.Ingredients.Single().FoodName, recipe.Ingredients.Single().Quantity));
        Assert.Equal((0.50m, true), (recipe.MinCostPerServing, recipe.CostComplete));
        Assert.Empty(await contract.RecipesAsync(household, [other.Id], CancellationToken.None));
    }

    [Fact]
    public async Task The_health_contract_reads_sleep_and_recovery_of_one_account_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var user = factory.CreateClient().AsUser(Guid.NewGuid());
        using var stranger = factory.CreateClient().AsUser(Guid.NewGuid());
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        foreach (var (client, seconds) in new[] { (user, 25200), (stranger, 30000) })
        {
            var token = await Post<CreatedImportTokenDto>(client, "/api/import-tokens", new ImportTokenRequest(null, null));
            using var service = factory.CreateClient();
            service.DefaultRequestHeaders.Add(ImportTokenAuthentication.HeaderName, token.Token);
            var night = new WellnessInput(yesterday, null, null, seconds, null, null, null, null, 75, "good", 50, 60, null, null, null,
                "BALANCED", null, null, 70, null, null, 20, 6000, JsonDocument.Parse("{}").RootElement);
            (await service.PostAsJsonAsync("/api/import/garmin/wellness", new WellnessImportRequest([night]))).EnsureSuccessStatusCode();
        }

        List<(Guid Household, Guid Owner, int? Seconds)> rows;
        await using (var db = factory.CreateDbContext())
            rows = (await db.DailyWellness.Where(w => w.Date == yesterday).Select(w => new { w.HouseholdId, w.OwnerUserId, w.SleepSeconds })
                .ToListAsync()).Select(w => (w.HouseholdId, w.OwnerUserId, w.SleepSeconds)).ToList();
        var mine = rows.Single(r => r.Seconds == 25200);
        var theirs = rows.Single(r => r.Seconds == 30000);
        using var scope = factory.Services.CreateScope();
        var contract = scope.ServiceProvider.GetRequiredService<HealthReadContract>();

        var day = Assert.Single(await contract.SleepAndRecoveryAsync(mine.Household, mine.Owner, yesterday, yesterday, CancellationToken.None));
        Assert.Equal((25200, 70), (day.SleepSeconds, day.BodyBatteryWake));
        Assert.Empty(await contract.SleepAndRecoveryAsync(mine.Household, theirs.Owner, yesterday, yesterday, CancellationToken.None));
    }
}
