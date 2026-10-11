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
    [InlineData("/api/articles")]
    [InlineData("/api/meal-components")]
    [InlineData("/api/composite-dishes")]
    public async Task Removed_catalogs_and_features_have_no_active_routes(string path)
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }
}
