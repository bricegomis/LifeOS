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
}
