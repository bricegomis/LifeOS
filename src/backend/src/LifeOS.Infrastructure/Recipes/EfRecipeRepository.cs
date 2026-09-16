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
        var existingRecipe = await _dbContext.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipe.Id, cancellationToken);

        if (existingRecipe != null)
        {
            _dbContext.Entry(existingRecipe).CurrentValues.SetValues(recipe);

            var currentIngredientIds = recipe.Ingredients.Select(i => i.Id).ToHashSet();
            var toRemove = existingRecipe.Ingredients.Where(i => !currentIngredientIds.Contains(i.Id)).ToList();
            foreach (var rem in toRemove)
            {
                _dbContext.Set<RecipeIngredient>().Remove(rem);
            }

            var existingIngredientDict = existingRecipe.Ingredients.ToDictionary(i => i.Id);
            foreach (var ing in recipe.Ingredients)
            {
                if (existingIngredientDict.TryGetValue(ing.Id, out var existingIng))
                {
                    _dbContext.Entry(existingIng).CurrentValues.SetValues(ing);
                }
                else
                {
                    existingRecipe.Ingredients.Add(ing);
                }
            }
        }
        else
        {
            _dbContext.Recipes.Update(recipe);
        }

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
