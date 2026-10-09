using LifeOS.Domain.FoodItems;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Articles;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Articles;

/// <summary>
/// EF Core / PostgreSQL implementation of <see cref="IArticleRepository"/>, isolated by household
/// (ADR 0003).
/// </summary>
public sealed class EfArticleRepository(LifeOSDbContext dbContext) : IArticleRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<FoodItem>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.GroceryItems
            .AsNoTracking()
            .Where(article => article.HouseholdId == householdId)
            .ToListAsync(cancellationToken);
    }

    public async Task<FoodItem?> GetByIdAsync(Guid householdId, Guid articleId, CancellationToken cancellationToken = default)
    {
        articleId = await _dbContext.ResolveProductIdAsync(householdId, articleId, cancellationToken);
        return await _dbContext.GroceryItems
            .FirstOrDefaultAsync(article => article.HouseholdId == householdId && article.Id == articleId, cancellationToken);
    }

    public async Task AddAsync(FoodItem article, CancellationToken cancellationToken = default)
    {
        _dbContext.GroceryItems.Add(article);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(FoodItem article, CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> DeleteAsync(Guid householdId, Guid articleId, CancellationToken cancellationToken = default)
    {
        articleId = await _dbContext.ResolveProductIdAsync(householdId, articleId, cancellationToken);
        var article = await _dbContext.GroceryItems
            .FirstOrDefaultAsync(item => item.HouseholdId == householdId && item.Id == articleId, cancellationToken);

        if (article is null)
        {
            return false;
        }

        article.Archive();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
