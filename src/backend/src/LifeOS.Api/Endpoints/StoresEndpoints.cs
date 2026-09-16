using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Application.Households;
using LifeOS.Application.Stores;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the household-scoped grocery stores endpoints.
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

        group.MapGet("/{storeId:guid}", GetStoreAsync)
            .WithName("GetStore");

        group.MapPost("/", CreateStoreAsync)
            .WithName("CreateStore");

        group.MapPut("/{storeId:guid}", UpdateStoreAsync)
            .WithName("UpdateStore");

        group.MapDelete("/{storeId:guid}", DeleteStoreAsync)
            .WithName("DeleteStore");

        return app;
    }

    private static async Task<IResult> GetStoresAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetStoresQuery getStoresQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var stores = await getStoresQuery.ExecuteAsync(householdId, cancellationToken);

        return Results.Ok(stores);
    }

    private static async Task<IResult> GetStoreAsync(
        ClaimsPrincipal user,
        Guid storeId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetStoreQuery getStoreQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var store = await getStoreQuery.ExecuteAsync(householdId, storeId, cancellationToken);

        return store is null ? Results.NotFound() : Results.Ok(store);
    }

    private static async Task<IResult> CreateStoreAsync(
        ClaimsPrincipal user,
        StoreRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        CreateStoreCommand createStoreCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var store = await createStoreCommand.ExecuteAsync(
                householdId,
                request.Name,
                request.Address,
                request.IsOrganic,
                request.IsLocal,
                cancellationToken);

            return Results.Created($"/api/stores/{store.Id}", store);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdateStoreAsync(
        ClaimsPrincipal user,
        Guid storeId,
        StoreRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        UpdateStoreCommand updateStoreCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var store = await updateStoreCommand.ExecuteAsync(
                householdId,
                storeId,
                request.Name,
                request.Address,
                request.IsOrganic,
                request.IsLocal,
                cancellationToken);

            return store is null ? Results.NotFound() : Results.Ok(store);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> DeleteStoreAsync(
        ClaimsPrincipal user,
        Guid storeId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        DeleteStoreCommand deleteStoreCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await deleteStoreCommand.ExecuteAsync(householdId, storeId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
