using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Stock;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Stock;

/// <summary>
/// EF Core implementation of <see cref="IStockItemRepository"/>.
/// </summary>
internal sealed class EfStockItemRepository(LifeOSDbContext dbContext) : IStockItemRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<StockItem>> GetAllForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StockItems
            .Where(item => item.HouseholdId == householdId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<StockItem?> GetByIdAsync(
        Guid householdId,
        Guid stockItemId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StockItems
            .FirstOrDefaultAsync(
                item => item.Id == stockItemId && item.HouseholdId == householdId,
                cancellationToken);
    }

    public async Task AddAsync(StockItem stockItem, CancellationToken cancellationToken = default)
    {
        _dbContext.StockItems.Add(stockItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(StockItem stockItem, CancellationToken cancellationToken = default)
    {
        _dbContext.StockItems.Update(stockItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid householdId,
        Guid stockItemId,
        CancellationToken cancellationToken = default)
    {
        var stockItem = await GetByIdAsync(householdId, stockItemId, cancellationToken);

        if (stockItem == null)
        {
            return false;
        }

        _dbContext.StockItems.Remove(stockItem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
