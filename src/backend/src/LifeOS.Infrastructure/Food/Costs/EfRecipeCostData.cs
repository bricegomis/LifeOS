using LifeOS.Application.Food.Costs;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Food.Costs;

internal sealed class EfRecipeCostData(LifeOSDbContext db) : IRecipeCostData
{
    public async Task<IReadOnlyList<Recipe>> RecipesAsync(Guid householdId, Guid? recipeId, CancellationToken ct)
    {
        var query = db.Recipes.AsNoTracking().Include(recipe => recipe.Ingredients)
            .Where(recipe => recipe.HouseholdId == householdId && !recipe.IsArchived);
        if (recipeId is { } id) query = query.Where(recipe => recipe.Id == id);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Product>> ProductsOfFoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct) =>
        await db.Products.AsNoTracking().Include(product => product.Prices)
            .Where(product => product.HouseholdId == householdId && foodIds.Contains(product.FoodItemId))
            .ToListAsync(ct);
}
