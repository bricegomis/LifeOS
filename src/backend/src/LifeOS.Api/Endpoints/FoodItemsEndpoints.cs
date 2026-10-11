using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Api.Validation;
using LifeOS.Application.FoodItems;
using LifeOS.Application.Households;
using LifeOS.Domain.FoodItems;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public static class FoodItemsEndpoints
{
    public static IEndpointRouteBuilder MapFoodItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/foods").RequireAuthorization().WithTags("Aliments").AddRequestValidation();
        g.MapGet("/", async (ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var foods = await db.FoodItems.Where(f => f.HouseholdId == household).OrderBy(f => f.Name).ToListAsync(ct);
            return Results.Ok(foods.Select(FoodItemMapper.ToDto));
        });
        g.MapPost("/", async (FoodRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var food = FoodItem.CreateManual(await households.ExecuteAsync(sub, ct), request.Name, request.ReferenceUnit ?? "",
                FoodItemMapper.NutritionFromDto(request.Nutrition));
            food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
            db.FoodItems.Add(food);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/foods/{food.Id}", FoodItemMapper.ToDto(food));
        });
        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            return food is null ? Results.NotFound() : Results.Ok(FoodItemMapper.ToDto(food));
        });
        g.MapPut("/{id:guid}", async (Guid id, FoodRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            if (food is null) return Results.NotFound();
            food.UpdateDetails(request.Name, request.ReferenceUnit ?? "", FoodItemMapper.NutritionFromDto(request.Nutrition));
            food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
            await db.SaveChangesAsync(ct);
            return Results.Ok(FoodItemMapper.ToDto(food));
        });
        g.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            if (food is null) return Results.NotFound();
            food.Archive();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        return app;
    }
}
