using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Stock;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Stock;

/// <summary>
/// EF Core implementation of <see cref="IShoppingListItemRepository"/>.
/// </summary>
internal sealed class EfShoppingListItemRepository(LifeOSDbContext dbContext) : IShoppingListItemRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ShoppingListItem>> GetAllForHouseholdAsync(
        Guid householdId,
        Guid? weekId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ShoppingListItems
            .Where(item => item.HouseholdId == householdId)
            .AsNoTracking();

        if (weekId.HasValue)
        {
            query = query.Where(item => item.WeekId == weekId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<ShoppingListItem?> GetByIdAsync(
        Guid householdId,
        Guid shoppingListItemId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShoppingListItems
            .FirstOrDefaultAsync(
                item => item.Id == shoppingListItemId && item.HouseholdId == householdId,
                cancellationToken);
    }

    public async Task AddAsync(ShoppingListItem shoppingListItem, CancellationToken cancellationToken = default)
    {
        _dbContext.ShoppingListItems.Add(shoppingListItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ShoppingListItem> shoppingListItems, CancellationToken cancellationToken = default)
    {
        _dbContext.ShoppingListItems.AddRange(shoppingListItems);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ShoppingListItem shoppingListItem, CancellationToken cancellationToken = default)
    {
        _dbContext.ShoppingListItems.Update(shoppingListItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(ShoppingListItem shoppingListItem, CancellationToken cancellationToken = default)
    {
        _dbContext.ShoppingListItems.Remove(shoppingListItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveRangeAsync(IEnumerable<ShoppingListItem> shoppingListItems, CancellationToken cancellationToken = default)
    {
        _dbContext.ShoppingListItems.RemoveRange(shoppingListItems);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShoppingListItem>> GetAllForWeekAsync(
        Guid householdId,
        Guid weekId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShoppingListItems
            .Where(item => item.HouseholdId == householdId && item.WeekId == weekId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
