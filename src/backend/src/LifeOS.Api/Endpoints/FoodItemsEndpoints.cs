using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Application.FoodItems;
using LifeOS.Application.Households;
using LifeOS.Domain.FoodItems;
using LifeOS.Infrastructure.FoodItems.OpenFoodFacts;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the food items endpoints (CRUD, OFF search, corrections) — Jalon 3.
/// </summary>
public static class FoodItemsEndpoints
{
    public static IEndpointRouteBuilder MapFoodItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/food-items")
            .WithTags("Food Items")
            .RequireAuthorization();

        group.MapGet("/", GetFoodItemsAsync)
            .WithName("GetFoodItems");

        group.MapPost("/", CreateFoodItemAsync)
            .WithName("CreateFoodItem");

        group.MapPut("/{foodItemId:guid}", UpdateFoodItemAsync)
            .WithName("UpdateFoodItem");

        group.MapDelete("/{foodItemId:guid}", DeleteFoodItemAsync)
            .WithName("DeleteFoodItem");

        group.MapGet("/search-off", SearchOpenFoodFactsByNameAsync)
            .WithName("SearchOpenFoodFactsByName");

        group.MapGet("/search-off-barcode", SearchOpenFoodFactsByBarcodeAsync)
            .WithName("SearchOpenFoodFactsByBarcode");

        group.MapPost("/{foodItemId:guid}/correction", CreateCorrectionAsync)
            .WithName("CreateFoodItemCorrection");

        return app;
    }

    private static async Task<IResult> GetFoodItemsAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IFoodItemRepository foodItemRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var items = await foodItemRepository.GetByHouseholdAsync(householdId, cancellationToken);

        return Results.Ok(items.Select(FoodItemMapper.ToDto).ToList());
    }

    private static async Task<IResult> CreateFoodItemAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IFoodItemRepository foodItemRepository,
        CreateFoodItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var nutrition = FoodItemMapper.NutritionFromDto(request.Nutrition);
            var foodItem = FoodItem.CreateManual(householdId, request.Name, request.ReferenceUnit, nutrition);
            await foodItemRepository.AddAsync(foodItem, cancellationToken);

            return Results.Created($"/api/food-items/{foodItem.Id}", FoodItemMapper.ToDto(foodItem));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdateFoodItemAsync(
        Guid foodItemId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IFoodItemRepository foodItemRepository,
        UpdateFoodItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var foodItem = await foodItemRepository.GetByIdAsync(foodItemId, cancellationToken);

        if (foodItem == null || foodItem.HouseholdId != householdId)
            return Results.NotFound();

        try
        {
            var nutrition = FoodItemMapper.NutritionFromDto(request.Nutrition);
            foodItem.UpdateDetails(request.Name, request.ReferenceUnit, nutrition);
            await foodItemRepository.UpdateAsync(foodItem, cancellationToken);

            return Results.Ok(FoodItemMapper.ToDto(foodItem));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> DeleteFoodItemAsync(
        Guid foodItemId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IFoodItemRepository foodItemRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var foodItem = await foodItemRepository.GetByIdAsync(foodItemId, cancellationToken);

        if (foodItem == null || foodItem.HouseholdId != householdId)
            return Results.NotFound();

        await foodItemRepository.DeleteAsync(foodItemId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SearchOpenFoodFactsByNameAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IOpenFoodFactsService offService,
        IFoodItemRepository foodItemRepository,
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest("Query parameter is required");
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var results = await offService.SearchByNameAsync(query, cancellationToken);
        var dtos = new List<FoodItemDto>();

        foreach (var product in results)
        {
            if (string.IsNullOrWhiteSpace(product.Code))
                continue;

            // Check if already cached
            var existing = await foodItemRepository.FindByOffBarcodeAsync(householdId, product.Code, cancellationToken);
            if (existing != null)
            {
                dtos.Add(FoodItemMapper.ToDto(existing));
                continue;
            }

            // Create and cache new item from OFF
            var (calories, protein, carbs, fat) = product.GetNutrients();
            var nutrition = new Nutrition(calories, protein, carbs, fat);
            var offPayload = System.Text.Json.JsonSerializer.Serialize(product);

            var foodItem = FoodItem.CreateFromOpenFoodFacts(
                householdId,
                product.GetName(),
                "100g",
                nutrition,
                product.Code,
                offPayload);

            await foodItemRepository.AddAsync(foodItem, cancellationToken);
            dtos.Add(FoodItemMapper.ToDto(foodItem));
        }

        return Results.Ok(dtos);
    }

    private static async Task<IResult> SearchOpenFoodFactsByBarcodeAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IOpenFoodFactsService offService,
        IFoodItemRepository foodItemRepository,
        [FromQuery] string barcode,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Results.BadRequest("Barcode parameter is required");
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        // Check if already cached
        var existing = await foodItemRepository.FindByOffBarcodeAsync(householdId, barcode, cancellationToken);
        if (existing != null)
            return Results.Ok(FoodItemMapper.ToDto(existing));

        // Query OFF
        var product = await offService.SearchByBarcodeAsync(barcode, cancellationToken);
        if (product == null || string.IsNullOrWhiteSpace(product.Code))
            return Results.NotFound();

        // Create and cache
        var (calories, protein, carbs, fat) = product.GetNutrients();
        var nutrition = new Nutrition(calories, protein, carbs, fat);
        var offPayload = System.Text.Json.JsonSerializer.Serialize(product);

        var foodItem = FoodItem.CreateFromOpenFoodFacts(
            householdId,
            product.GetName(),
            "100g",
            nutrition,
            product.Code,
            offPayload);

        await foodItemRepository.AddAsync(foodItem, cancellationToken);
        return Results.Ok(FoodItemMapper.ToDto(foodItem));
    }

    private static async Task<IResult> CreateCorrectionAsync(
        Guid foodItemId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IFoodItemRepository foodItemRepository,
        CreateCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var original = await foodItemRepository.GetByIdAsync(foodItemId, cancellationToken);

        if (original == null || original.HouseholdId != householdId)
            return Results.NotFound();

        try
        {
            var nutrition = FoodItemMapper.NutritionFromDto(request.Nutrition);
            var correction = FoodItem.CreateCorrection(householdId, request.Name, request.ReferenceUnit, nutrition, foodItemId);
            await foodItemRepository.AddAsync(correction, cancellationToken);

            return Results.Created($"/api/food-items/{correction.Id}", FoodItemMapper.ToDto(correction));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
