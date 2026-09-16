using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Stores;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Stores;

/// <summary>
/// EF Core / PostgreSQL implementation of <see cref="IStoreRepository"/>, isolated by household
/// (ADR 0003).
/// </summary>
public sealed class EfStoreRepository(LifeOSDbContext dbContext) : IStoreRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<Store>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Stores
            .AsNoTracking()
            .Where(store => store.HouseholdId == householdId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Store?> GetByIdAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Stores
            .FirstOrDefaultAsync(store => store.Id == storeId && store.HouseholdId == householdId, cancellationToken);
    }

    public async Task AddAsync(Store store, CancellationToken cancellationToken = default)
    {
        await _dbContext.Stores.AddAsync(store, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Store store, CancellationToken cancellationToken = default)
    {
        _dbContext.Stores.Update(store);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken = default)
    {
        var store = await GetByIdAsync(householdId, storeId, cancellationToken);

        if (store is null)
        {
            return false;
        }

        _dbContext.Stores.Remove(store);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
