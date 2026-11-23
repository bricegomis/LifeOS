using LifeOS.Application.Food.Contracts;
using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Food;

internal sealed class EfFoodReadData(LifeOSDbContext db) : IFoodReadData
{
    public async Task<IReadOnlyList<Recipe>> RecipesAsync(Guid householdId, IReadOnlyCollection<Guid>? recipeIds, CancellationToken ct)
    {
        var query = db.Recipes.AsNoTracking().Include(r => r.Ingredients).Where(r => r.HouseholdId == householdId && !r.IsArchived);
        if (recipeIds is not null) query = query.Where(r => recipeIds.Contains(r.Id));
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FoodItem>> FoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct) =>
        await db.FoodItems.AsNoTracking().Where(f => f.HouseholdId == householdId && foodIds.Contains(f.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> ProductsOfFoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct) =>
        await db.Products.AsNoTracking().Include(p => p.Prices)
            .Where(p => p.HouseholdId == householdId && foodIds.Contains(p.FoodItemId))
            .ToListAsync(ct);
}
