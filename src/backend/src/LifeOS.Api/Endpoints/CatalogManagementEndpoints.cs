using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public static class CatalogManagementEndpoints
{
    public record ArticleLinkRequest(Guid? ArticleId);
    public record IngredientEditRequest(decimal Quantity, string Unit);

    public static IEndpointRouteBuilder MapCatalogManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization().WithTags("Catalog management");
        group.MapPut("/food-items/{id:guid}/article", async (Guid id, ArticleLinkRequest request,
            ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household, ct);
            if (food is null) return Results.NotFound();
            if (request.ArticleId is { } article)
            {
                if (!await db.GroceryItems.AnyAsync(a => a.Id == article && a.HouseholdId == household, ct))
                    return Results.Problem("Article introuvable dans ce foyer.", statusCode: 400);
                if (await db.FoodItems.AnyAsync(f => f.Id != id && f.ArticleId == article, ct))
                    return Results.Problem("Cet article est déjà relié à un produit.", statusCode: 409);
            }
            food.LinkArticle(request.ArticleId);
            await db.SaveChangesAsync(ct);
            return Results.Ok(LifeOS.Application.FoodItems.FoodItemMapper.ToDto(food));
        });
        group.MapPut("/recipes/{id:guid}/ingredients/{ingredientId:guid}", async (Guid id, Guid ingredientId,
            IngredientEditRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households,
            LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var recipe = await db.Recipes.Include(r => r.Ingredients)
                .SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == household, ct);
            var ingredient = recipe?.Ingredients.SingleOrDefault(i => i.Id == ingredientId);
            if (ingredient is null) return Results.NotFound();
            ingredient.Update(request.Quantity, request.Unit);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        group.MapDelete("/recipes/{id:guid}/ingredients/{ingredientId:guid}", async (Guid id, Guid ingredientId,
            ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var recipe = await db.Recipes.Include(r => r.Ingredients)
                .SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == household, ct);
            var ingredient = recipe?.Ingredients.SingleOrDefault(i => i.Id == ingredientId);
            if (ingredient is null) return Results.NotFound();
            db.RecipeIngredients.Remove(ingredient);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        return app;
    }
}
