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
}
