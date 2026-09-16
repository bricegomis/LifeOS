using LifeOS.Domain.ComposedMeals;

namespace LifeOS.Application.ComposedMeals;

public interface IComposedMealRepository
{
    Task<ComposedMeal?> GetByIdAsync(Guid mealId, Guid householdId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComposedMeal>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<ComposedMeal> AddAsync(ComposedMeal meal, CancellationToken cancellationToken = default);
    Task<ComposedMeal> UpdateAsync(ComposedMeal meal, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid mealId, Guid householdId, CancellationToken cancellationToken = default);
}
