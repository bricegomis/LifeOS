using System.Net;
using System.Text.Json;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class OpenApiTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task OpenApi_document_is_available_and_contains_paths()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("openapi").GetString()));
        Assert.Equal(JsonValueKind.Object, root.GetProperty("info").ValueKind);

        var paths = root.GetProperty("paths");
        Assert.Equal(JsonValueKind.Object, paths.ValueKind);
        Assert.NotEmpty(paths.EnumerateObject());
    }
}
