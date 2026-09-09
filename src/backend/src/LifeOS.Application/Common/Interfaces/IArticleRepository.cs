using LifeOS.Domain.Articles;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="GroceryItem"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IArticleRepository
{
    Task<IReadOnlyList<GroceryItem>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    Task<GroceryItem?> GetByIdAsync(Guid ownerId, Guid articleId, CancellationToken cancellationToken = default);

    Task AddAsync(GroceryItem article, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid ownerId, Guid articleId, CancellationToken cancellationToken = default);
}
