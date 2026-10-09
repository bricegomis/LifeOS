using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Api.Validation;
using LifeOS.Application.FoodItems;
using LifeOS.Application.Households;
using LifeOS.Domain.FoodItems;
using LifeOS.Infrastructure.FoodItems.OpenFoodFacts;
using Microsoft.AspNetCore.Mvc;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the food items endpoints (CRUD, OFF search, corrections) — Jalon 3.
/// </summary>
public static class FoodItemsEndpoints
{
    public static IEndpointRouteBuilder MapFoodItemsEndpoints(this IEndpointRouteBuilder app)
    {
        MapProductRoutes(app);
        var group = app.MapGroup("/api/food-items")
            .WithTags("Food Items")
            .RequireAuthorization()
            .AddRequestValidation();

        group.MapGet("/", GetFoodItemsAsync)
            .WithName("GetFoodItems")
            .Produces<List<FoodItemDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", CreateFoodItemAsync)
            .WithName("CreateFoodItem")
            .Produces<FoodItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/search-off", SearchOpenFoodFactsByNameAsync)
            .WithName("SearchOpenFoodFactsByName")
            .Produces<List<FoodItemDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/search-off-barcode", SearchOpenFoodFactsByBarcodeAsync)
            .WithName("SearchOpenFoodFactsByBarcode")
            .Produces<FoodItemDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{foodItemId:guid}", GetFoodItemByIdAsync)
            .WithName("GetFoodItemById")
            .Produces<FoodItemDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{foodItemId:guid}", UpdateFoodItemAsync)
            .WithName("UpdateFoodItem")
            .Produces<FoodItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{foodItemId:guid}", DeleteFoodItemAsync)
            .WithName("DeleteFoodItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{foodItemId:guid}/correction", CreateCorrectionAsync)
            .WithName("CreateFoodItemCorrection")
            .Produces<FoodItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static void MapProductRoutes(IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products").RequireAuthorization().AddRequestValidation();
        products.MapGet("/", GetFoodItemsAsync);
        products.MapGet("/{foodItemId:guid}", GetFoodItemByIdAsync);
        products.MapGet("/{foodItemId:guid}/usage", GetProductUsageAsync);
        products.MapPost("/", CreateFoodItemAsync);
        products.MapPut("/{foodItemId:guid}", UpdateFoodItemAsync);
        products.MapDelete("/{foodItemId:guid}", DeleteFoodItemAsync);
        products.MapGet("/search-off", SearchOpenFoodFactsByNameAsync);
        products.MapGet("/search-off-barcode", SearchOpenFoodFactsByBarcodeAsync);
        products.MapPost("/{foodItemId:guid}/correction", CreateCorrectionAsync);
    }

    public sealed record ProductRecipeUsage(Guid Id, string Name, bool IsArchived);
    public sealed record ProductStockUsage(decimal Quantity, string Unit);
    public sealed record ProductUsageDto(IReadOnlyList<ProductRecipeUsage> Recipes,
        IReadOnlyList<ProductStockUsage> Stock, int MealOccurrences, int HistoricalShoppingLines);

    private static async Task<IResult> GetProductUsageAsync(Guid foodItemId, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(sub, ct);
        if (!await db.FoodItems.AnyAsync(p => p.Id == foodItemId && p.HouseholdId == household, ct))
            return Results.NotFound();
        var recipes = await db.Recipes.Where(r => r.HouseholdId == household && r.Ingredients.Any(i => i.FoodItemId == foodItemId))
            .OrderBy(r => r.Name).Select(r => new ProductRecipeUsage(r.Id, r.Name, r.IsArchived)).ToListAsync(ct);
        var stock = await db.StockItems.Where(s => s.HouseholdId == household && s.GroceryItemId == foodItemId)
            .Select(s => new ProductStockUsage(s.Quantity, s.Unit)).ToListAsync(ct);
        var meals = await db.MealFoodLines.Where(l => l.FoodItemId == foodItemId
                && db.PlannedMeals.Any(m => m.Id == l.PlannedMealId
                    && db.DayPlans.Any(d => d.Id == m.DayPlanId && db.Weeks.Any(w => w.Id == d.WeekId && w.HouseholdId == household))))
            .Select(l => l.PlannedMealId).Distinct().CountAsync(ct);
        var shopping = await db.ShoppingListItems.CountAsync(s => s.HouseholdId == household && s.GroceryItemId == foodItemId, ct);
        return Results.Ok(new ProductUsageDto(recipes, stock, meals, shopping));
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

    private static async Task<IResult> GetFoodItemByIdAsync(
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
        {
            return Results.NotFound();
        }

        return Results.Ok(FoodItemMapper.ToDto(foodItem));
    }

    private static async Task<IResult> CreateFoodItemAsync(
        HttpContext context,
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
            if (context.Request.Path.StartsWithSegments("/api/products") && request.Unit is null)
                throw new ArgumentException("Précisez l'unité d'achat du produit.");
            var nutrition = FoodItemMapper.NutritionFromDto(request.Nutrition);
            var foodItem = FoodItem.CreateManual(householdId, request.Name, request.ReferenceUnit ?? "", nutrition);
            if (request.Unit is not null)
                foodItem.UpdatePurchaseDetails(request.Description ?? "", LifeOS.Application.Articles.ArticleMapper.ParseUnit(request.Unit));
            await foodItemRepository.AddAsync(foodItem, cancellationToken);

            return Results.Created($"/api/products/{foodItem.Id}", FoodItemMapper.ToDto(foodItem));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
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
            foodItem.UpdateDetails(request.Name, request.ReferenceUnit ?? "", nutrition);
            if (request.Unit is not null)
                foodItem.UpdatePurchaseDetails(request.Description ?? foodItem.Description, LifeOS.Application.Articles.ArticleMapper.ParseUnit(request.Unit));
            await foodItemRepository.UpdateAsync(foodItem, cancellationToken);

            return Results.Ok(FoodItemMapper.ToDto(foodItem));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
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

        foodItem.Archive();
        await foodItemRepository.UpdateAsync(foodItem, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SearchOpenFoodFactsByNameAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IOpenFoodFactsService offService,
        IFoodItemRepository foodItemRepository,
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.Problem("Name parameter is required", statusCode: StatusCodes.Status400BadRequest);
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var results = await offService.SearchByNameAsync(name, cancellationToken);
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
            return Results.Problem("Barcode parameter is required", statusCode: StatusCodes.Status400BadRequest);
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
            var correction = FoodItem.CreateCorrection(householdId, request.Name, request.ReferenceUnit ?? "", nutrition, foodItemId);
            if (request.Unit is not null)
                correction.UpdatePurchaseDetails(request.Description ?? original.Description, LifeOS.Application.Articles.ArticleMapper.ParseUnit(request.Unit));
            else if (original.PurchaseUnitConfirmed)
                correction.UpdatePurchaseDetails(request.Description ?? original.Description, original.Unit);
            await foodItemRepository.AddAsync(correction, cancellationToken);

            return Results.Created($"/api/food-items/{correction.Id}", FoodItemMapper.ToDto(correction));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
