using LifeOS.Domain.Articles;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="GroceryItem"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IArticleRepository
{
    Task<IReadOnlyList<GroceryItem>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task<GroceryItem?> GetByIdAsync(Guid householdId, Guid articleId, CancellationToken cancellationToken = default);

    Task AddAsync(GroceryItem article, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes made to an article previously loaded via <see cref="GetByIdAsync"/>.
    /// </summary>
    Task UpdateAsync(GroceryItem article, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid householdId, Guid articleId, CancellationToken cancellationToken = default);
}
