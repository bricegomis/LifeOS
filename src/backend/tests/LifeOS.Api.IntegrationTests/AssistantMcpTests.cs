using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Authentication;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0011: the MCP endpoint, its OAuth discovery and the connected assistants.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AssistantMcpTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Discovery_lets_Claude_use_its_client_metadata_document_as_a_public_client()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/.well-known/oauth-authorization-server"));
        Assert.True(document.RootElement.GetProperty("client_id_metadata_document_supported").GetBoolean());
        Assert.Equal(["none"], document.RootElement.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Without_a_token_the_MCP_endpoint_points_Claude_to_its_resource_metadata()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/mcp", new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/mcp\"", challenge);

        using var metadata = JsonDocument.Parse(await client.GetStringAsync("/.well-known/oauth-protected-resource/mcp"));
        Assert.Equal("http://localhost/mcp", metadata.RootElement.GetProperty("resource").GetString());
        Assert.Equal(["http://localhost/"], metadata.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task The_assistant_lists_its_tools_and_reads_the_callers_household_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var userA = Guid.NewGuid();
        var http = factory.CreateClient().AsUser(userA);
        (await http.PostAsync("/api/households/profiles", JsonContent(new { displayName = "Élise", portionCoefficient = 0.5m }))).EnsureSuccessStatusCode();
        // Another household exists with its own profile.
        (await factory.CreateClient().AsUser(Guid.NewGuid()).PostAsync("/api/households/profiles",
            JsonContent(new { displayName = "Voisin", portionCoefficient = 1m }))).EnsureSuccessStatusCode();

        await using var mcp = await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            http));

        var tools = await mcp.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "get_household");

        var result = await mcp.CallToolAsync("get_household");
        Assert.NotEqual(true, result.IsError);
        var text = string.Join("", result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(block => block.Text));
        Assert.Contains("Élise", text);
        Assert.DoesNotContain("Voisin", text);
    }

    [Fact]
    public async Task Connected_assistants_are_listed_and_revoked_by_their_owner_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var owner = Guid.NewGuid();
        var ownerClient = factory.CreateClient().AsUser(owner);
        var otherClient = factory.CreateClient().AsUser(Guid.NewGuid());
        await ownerClient.GetAsync("/api/households/current");

        string authorizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var application = await applications.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = $"https://claude.ai/test-{Guid.NewGuid():N}",
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Explicit,
                DisplayName = "Claude (claude.ai)",
            });
            var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = await applications.GetIdAsync(application),
                Subject = owner.ToString(),
                Status = Statuses.Valid,
                Type = AuthorizationTypes.Permanent,
                CreationDate = DateTimeOffset.UtcNow,
            });
            authorizationId = (await authorizations.GetIdAsync(authorization))!;
        }

        var listed = await ownerClient.GetFromJsonAsync<List<AssistantConnectionDto>>("/api/account/connections");
        Assert.Equal("Claude (claude.ai)", Assert.Single(listed!).ClientName);
        Assert.Empty((await otherClient.GetFromJsonAsync<List<AssistantConnectionDto>>("/api/account/connections"))!);

        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.DeleteAsync($"/api/account/connections/{authorizationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.DeleteAsync("/api/account/connections/not-a-guid")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.DeleteAsync($"/api/account/connections/{authorizationId}")).StatusCode);
        Assert.Empty((await ownerClient.GetFromJsonAsync<List<AssistantConnectionDto>>("/api/account/connections"))!);
    }

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");
}
