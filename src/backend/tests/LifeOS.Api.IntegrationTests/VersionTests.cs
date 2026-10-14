using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LifeOS.Api.IntegrationTests;

// Version diagnostics do not touch the database, so test them without a PostgreSQL container.
public sealed class VersionTests
{
    [Theory]
    [InlineData(null, null, null, "local")]
    [InlineData("", "", "", "")]
    [InlineData("123", "987654321", "1", "abcdef0123456789")]
    [InlineData("123", "987654321", "2", "abcdef0123456789")]
    public async Task Version_returns_its_own_artifact_metadata_without_authentication_or_caching(
        string? number, string? runId, string? attempt, string commit)
    {
        var hasCiBuild = !string.IsNullOrWhiteSpace(number);
        await using var factory = new VersionApiFactory(new Dictionary<string, string?>
        {
            ["LifeOS:BuildId"] = commit,
            ["LifeOS:BuildNumber"] = number,
            ["LifeOS:RunId"] = runId,
            ["LifeOS:RunAttempt"] = attempt,
            ["LifeOS:Repository"] = hasCiBuild ? "bricegomis/LifeOS" : null,
            ["LifeOS:ServerUrl"] = hasCiBuild ? "https://github.com" : null,
            ["LifeOS:Workflow"] = hasCiBuild ? "Build & Push Docker Images" : null,
        });
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal("api", root.GetProperty("component").GetString());
        Assert.Equal(string.IsNullOrWhiteSpace(commit) ? "local" : commit, root.GetProperty("buildId").GetString());
        Assert.Equal(hasCiBuild ? number : null, root.GetProperty("buildNumber").GetString());
        Assert.Equal(hasCiBuild ? runId : null, root.GetProperty("runId").GetString());
        Assert.Equal(hasCiBuild ? attempt : null, root.GetProperty("runAttempt").GetString());
        Assert.Equal(hasCiBuild ? "bricegomis/LifeOS" : null, root.GetProperty("repository").GetString());
        Assert.Equal(hasCiBuild ? "https://github.com" : null, root.GetProperty("serverUrl").GetString());
        Assert.Equal(hasCiBuild ? "Build & Push Docker Images" : null, root.GetProperty("workflow").GetString());
    }

    private sealed class VersionApiFactory(Dictionary<string, string?> metadata) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                metadata["SkipDatabaseMigration"] = "true";
                metadata["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused";
                metadata["Supabase:Url"] = "https://test.supabase.co";
                config.AddInMemoryCollection(metadata);
            });
        }
    }
}
