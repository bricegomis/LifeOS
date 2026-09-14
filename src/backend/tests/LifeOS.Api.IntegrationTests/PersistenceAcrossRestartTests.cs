using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Application.Articles;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Proves the other core milestone 1 exit criterion: data survives a logical API restart. Two
/// independent <see cref="LifeOSApiFactory"/> instances are built against the *same* PostgreSQL
/// container (simulating the process being stopped and started again), and data written by the
/// first must be readable from the second.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PersistenceAcrossRestartTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Household_and_article_data_survive_an_api_restart()
    {
        var supabaseUserId = Guid.NewGuid();
        Guid articleId;

        await using (var firstRun = new LifeOSApiFactory(postgres.ConnectionString))
        {
            using var client = firstRun.CreateClient().AsUser(supabaseUserId);

            var createResponse = await client.PostAsJsonAsync(
                "/api/articles",
                new ArticleRequest("Riz", "Riz basmati", "kilogram"));

            createResponse.EnsureSuccessStatusCode();
            var created = await createResponse.Content.ReadFromJsonAsync<GroceryItemDto>();
            articleId = created!.Id;
        }
        // The first host (and its in-process app/DbContext pool) is now fully disposed, simulating
        // the API process stopping. Nothing but the PostgreSQL container survives below this line.

        await using var secondRun = new LifeOSApiFactory(postgres.ConnectionString);
        using var reconnectedClient = secondRun.CreateClient().AsUser(supabaseUserId);

        var articles = await reconnectedClient.GetFromJsonAsync<List<GroceryItemDto>>("/api/articles");

        Assert.Contains(articles!, a => a.Id == articleId && a.Name == "Riz");
    }

    [Fact]
    public async Task Household_provisioning_is_idempotent_across_restarts()
    {
        var supabaseUserId = Guid.NewGuid();
        Guid householdIdAfterFirstRun;

        await using (var firstRun = new LifeOSApiFactory(postgres.ConnectionString))
        {
            using var client = firstRun.CreateClient().AsUser(supabaseUserId);
            await client.GetAsync("/api/stores");

            await using var dbContext = firstRun.CreateDbContext();
            householdIdAfterFirstRun = dbContext.HouseholdMembers.Single(m => m.SupabaseUserId == supabaseUserId).HouseholdId;
        }

        await using var secondRun = new LifeOSApiFactory(postgres.ConnectionString);
        using var reconnectedClient = secondRun.CreateClient().AsUser(supabaseUserId);
        await reconnectedClient.GetAsync("/api/stores");

        await using var secondDbContext = secondRun.CreateDbContext();
        var householdIdAfterSecondRun = secondDbContext.HouseholdMembers
            .Single(m => m.SupabaseUserId == supabaseUserId).HouseholdId;

        Assert.Equal(householdIdAfterFirstRun, householdIdAfterSecondRun);
    }
}
