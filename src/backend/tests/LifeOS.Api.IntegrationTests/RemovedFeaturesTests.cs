using System.Net;
namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class RemovedFeaturesTests(PostgresContainerFixture postgres)
{
    [Theory]
    [InlineData("/api/stock-items")]
    [InlineData("/api/shopping-list/items")]
    [InlineData("/api/products/search-off")]
    [InlineData("/api/products/search-off-barcode")]
    [InlineData("/api/food-items")]
    [InlineData("/api/foods/prices")]
    [InlineData("/api/articles")]
    [InlineData("/api/meal-components")]
    [InlineData("/api/composite-dishes")]
    [InlineData("/api/activities")]
    [InlineData("/api/nutrition/configuration")]
    [InlineData("/api/nutrition/calculations/day/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/activity-sessions/day/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/manual-planner/weeks")]
    [InlineData("/api/manual-planner/weeks/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/reviews/planned-lines/00000000-0000-0000-0000-000000000001")]
    public async Task Removed_catalogs_and_features_have_no_active_routes(string path)
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }
}
