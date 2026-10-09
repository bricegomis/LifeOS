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
        group.MapPut("/food-items/{id:guid}/article", () =>
            Results.Problem("Aliments et articles sont désormais un seul produit. Modifiez sa fiche dans /api/products.", statusCode: 410));
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
