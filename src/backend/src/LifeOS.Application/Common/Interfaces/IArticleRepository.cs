using LifeOS.Domain.FoodItems;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="FoodItem"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IArticleRepository
{
    Task<IReadOnlyList<FoodItem>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task<FoodItem?> GetByIdAsync(Guid householdId, Guid articleId, CancellationToken cancellationToken = default);

    Task AddAsync(FoodItem article, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes made to an article previously loaded via <see cref="GetByIdAsync"/>.
    /// </summary>
    Task UpdateAsync(FoodItem article, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid householdId, Guid articleId, CancellationToken cancellationToken = default);
}
