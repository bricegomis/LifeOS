using LifeOS.Domain.Recipes;

namespace LifeOS.Application.Recipes;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recipe>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<Recipe> AddAsync(Recipe recipe, CancellationToken cancellationToken = default);
    Task<Recipe> UpdateAsync(Recipe recipe, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken = default);
}
