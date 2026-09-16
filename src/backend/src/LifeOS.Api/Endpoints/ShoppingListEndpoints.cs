using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
using LifeOS.Application.Stock;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.Api.Endpoints;

public static class ShoppingListEndpoints
{
    public static IEndpointRouteBuilder MapShoppingListEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shopping-list")
            .WithTags("ShoppingList")
            .RequireAuthorization()
            .AddRequestValidation();

        group.MapPost("/weeks/{weekId:guid}/generate", GenerateShoppingListAsync)
            .WithName("GenerateShoppingList")
            .Produces<GeneratedShoppingListResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/items", GetShoppingListItemsAsync)
            .WithName("GetShoppingListItems")
            .Produces<List<ShoppingListItemDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/items/{itemId:guid}", UpdateShoppingListItemCheckedAsync)
            .WithName("UpdateShoppingListItemChecked")
            .Produces<ShoppingListItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GenerateShoppingListAsync(
        ClaimsPrincipal user,
        Guid weekId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GenerateShoppingListCommand generateShoppingListCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var items = await generateShoppingListCommand.ExecuteAsync(householdId, weekId, cancellationToken);
            return Results.Ok(new GeneratedShoppingListResponse(items));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

    }

    private static async Task<IResult> GetShoppingListItemsAsync(
        ClaimsPrincipal user,
        [FromQuery] Guid? weekId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetShoppingListItemsQuery getShoppingListItemsQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var items = await getShoppingListItemsQuery.ExecuteAsync(householdId, weekId, cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> UpdateShoppingListItemCheckedAsync(
        ClaimsPrincipal user,
        Guid itemId,
        UpdateShoppingListItemRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        UpdateShoppingListItemCheckedCommand updateShoppingListItemCheckedCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var item = await updateShoppingListItemCheckedCommand.ExecuteAsync(
            householdId,
            itemId,
            request.Checked,
            cancellationToken);

        if (item == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(item);
    }
}

public sealed record GeneratedShoppingListResponse(List<ShoppingListItemDto> ShoppingList);
