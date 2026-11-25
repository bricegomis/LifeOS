using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// The API has no fallback authorization policy: every route says itself who may call it. This
/// test fails when a new route forgets <c>RequireAuthorization()</c> (or is public without saying so
/// with <c>AllowAnonymous()</c>), so household data can never be served to an anonymous caller.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EndpointAuthorizationTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Every_route_requires_a_signed_in_caller_or_is_explicitly_public()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString, enableOpenApiUi: false);
        using var _ = factory.CreateClient();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        Assert.NotEmpty(endpoints);

        var undecided = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null && !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(endpoint => endpoint.RoutePattern.RawText ?? endpoint.DisplayName ?? "?")
            .Order()
            .ToList();
        Assert.Empty(undecided);
    }

    [Fact]
    public async Task Only_sign_in_discovery_and_version_routes_are_public()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString, enableOpenApiUi: false);
        using var _ = factory.CreateClient();

        var anonymous = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText ?? "?")
            .Distinct()
            .Order()
            .ToList();
        Assert.All(anonymous, route => Assert.True(
            route == "/api/version" || route.StartsWith("/connect/", StringComparison.Ordinal)
                || route.StartsWith("/.well-known/", StringComparison.Ordinal) || route.StartsWith("/account/", StringComparison.Ordinal),
            $"Unexpected public route {route}"));
    }
}
