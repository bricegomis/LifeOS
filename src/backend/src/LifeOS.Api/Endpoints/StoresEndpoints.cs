using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Stores;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the read-only grocery stores endpoints.
/// </summary>
public static class StoresEndpoints
{
    public static IEndpointRouteBuilder MapStoresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stores")
            .WithTags("Stores")
            .RequireAuthorization();

        group.MapGet("/", GetStoresAsync)
            .WithName("GetStores");

        return app;
    }

    private static async Task<IResult> GetStoresAsync(
        ClaimsPrincipal user,
        GetStoresQuery getStoresQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        var stores = await getStoresQuery.ExecuteAsync(ownerId, cancellationToken);

        return Results.Ok(stores);
    }
}
