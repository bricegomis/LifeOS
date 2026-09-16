using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Application.Households;
using LifeOS.Application.Stock;

namespace LifeOS.Api.Endpoints;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock-items")
            .WithTags("Stock")
            .RequireAuthorization();

        group.MapGet("/", GetStockItemsAsync)
            .WithName("GetStockItems")
            .WithOpenApi();

        group.MapPost("/", CreateStockItemAsync)
            .WithName("CreateStockItem")
            .WithOpenApi();

        group.MapPut("/{stockItemId:guid}", UpdateStockItemAsync)
            .WithName("UpdateStockItem")
            .WithOpenApi();

        group.MapDelete("/{stockItemId:guid}", DeleteStockItemAsync)
            .WithName("DeleteStockItem")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetStockItemsAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetStockItemsQuery getStockItemsQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var items = await getStockItemsQuery.ExecuteAsync(householdId, cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateStockItemAsync(
        ClaimsPrincipal user,
        CreateStockItemRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        CreateStockItemCommand createStockItemCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var item = await createStockItemCommand.ExecuteAsync(
                householdId,
                request.GroceryItemId,
                request.Quantity,
                request.Unit,
                cancellationToken);

            return Results.Created($"/api/stock-items/{item.Id}", item);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdateStockItemAsync(
        ClaimsPrincipal user,
        Guid stockItemId,
        UpdateStockItemRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        UpdateStockItemCommand updateStockItemCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var item = await updateStockItemCommand.ExecuteAsync(
            householdId,
            stockItemId,
            request.Quantity,
            request.Unit ?? "",
            cancellationToken);

        if (item == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(item);
    }

    private static async Task<IResult> DeleteStockItemAsync(
        ClaimsPrincipal user,
        Guid stockItemId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        DeleteStockItemCommand deleteStockItemCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await deleteStockItemCommand.ExecuteAsync(householdId, stockItemId, cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }
}
