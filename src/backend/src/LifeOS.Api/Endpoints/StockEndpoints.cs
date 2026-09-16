using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
using LifeOS.Application.Stock;

namespace LifeOS.Api.Endpoints;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock-items")
            .WithTags("Stock")
            .RequireAuthorization()
            .AddRequestValidation();

        group.MapGet("/", GetStockItemsAsync)
            .WithName("GetStockItems")
            .Produces<List<StockItemDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", CreateStockItemAsync)
            .WithName("CreateStockItem")
            .Produces<StockItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{stockItemId:guid}", UpdateStockItemAsync)
            .WithName("UpdateStockItem")
            .Produces<StockItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{stockItemId:guid}", DeleteStockItemAsync)
            .WithName("DeleteStockItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

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
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
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
