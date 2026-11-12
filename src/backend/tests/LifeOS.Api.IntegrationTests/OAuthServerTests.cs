using System.Net;
using System.Text.Json;
using LifeOS.Api.Authentication;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0011: LifeOS is its own OAuth server; clients are registered up front.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OAuthServerTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Discovery_advertises_code_flow_with_PKCE_S256_and_no_dynamic_registration()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration"));
        var root = document.RootElement;

        Assert.EndsWith("/connect/authorize", root.GetProperty("authorization_endpoint").GetString());
        Assert.EndsWith("/connect/token", root.GetProperty("token_endpoint").GetString());
        Assert.Contains("S256", root.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain("plain", root.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(e => e.GetString()));
        Assert.False(root.TryGetProperty("registration_endpoint", out _));
    }

    [Fact]
    public async Task The_LifeOS_app_client_is_registered_at_startup_as_a_public_PKCE_client()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        await client.GetAsync("/api/version");

        await using var scope = factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(AuthOptions.WebClientOptions.ClientId);

        Assert.NotNull(application);
        Assert.Equal(OpenIddictConstants.ClientTypes.Public, await manager.GetClientTypeAsync(application));
        Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange, await manager.GetRequirementsAsync(application));
    }

    [Fact]
    public async Task An_unknown_client_is_refused_at_the_authorization_endpoint()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            "/connect/authorize?client_id=unknown&response_type=code&redirect_uri=https%3A%2F%2Fevil.example%2F" +
            "&scope=openid&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Api_routes_refuse_requests_without_a_token()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/households/current")).StatusCode);
    }
}
