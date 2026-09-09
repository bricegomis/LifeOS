using System.Collections.Concurrent;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Articles;

namespace LifeOS.Infrastructure.Articles;

/// <summary>
/// Temporary in-memory implementation of <see cref="IArticleRepository"/>.
/// This is the first, simplest persistence port; it is expected to be replaced by a real database
/// (e.g. PostgreSQL via EF Core) once the articles feature grows beyond simple CRUD.
/// </summary>
public sealed class InMemoryArticleRepository : IArticleRepository
{
    private readonly ConcurrentDictionary<Guid, List<GroceryItem>> _articlesByOwner = new();

    public Task<IReadOnlyList<GroceryItem>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var articles = _articlesByOwner.GetOrAdd(ownerId, _ => []);

        IReadOnlyList<GroceryItem> result = articles.ToList();
        return Task.FromResult(result);
    }

    public Task<GroceryItem?> GetByIdAsync(Guid ownerId, Guid articleId, CancellationToken cancellationToken = default)
    {
        var articles = _articlesByOwner.GetOrAdd(ownerId, _ => []);

        return Task.FromResult(articles.Find(article => article.Id == articleId));
    }

    public Task AddAsync(GroceryItem article, CancellationToken cancellationToken = default)
    {
        var articles = _articlesByOwner.GetOrAdd(article.OwnerId, _ => []);

        lock (articles)
        {
            articles.Add(article);
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid ownerId, Guid articleId, CancellationToken cancellationToken = default)
    {
        var articles = _articlesByOwner.GetOrAdd(ownerId, _ => []);

        lock (articles)
        {
            return Task.FromResult(articles.RemoveAll(article => article.Id == articleId) > 0);
        }
    }
}
