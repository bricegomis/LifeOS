using LifeOS.Application.Recipes;
using LifeOS.Domain.Recipes;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Recipes;

public sealed class EfRecipeRepository(LifeOSDbContext dbContext) : IRecipeRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<Recipe?> GetByIdAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId, cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.HouseholdId == householdId)
            .Include(r => r.Ingredients)
            .ToListAsync(cancellationToken);
    }

    public async Task<Recipe> AddAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        await _dbContext.Recipes.AddAsync(recipe, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return recipe;
    }

    public async Task<Recipe> UpdateAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        _dbContext.Recipes.Update(recipe);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return recipe;
    }

    public async Task<bool> DeleteAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken = default)
    {
        var recipe = await _dbContext.Recipes
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId, cancellationToken);

        if (recipe == null)
        {
            return false;
        }

        _dbContext.Recipes.Remove(recipe);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
