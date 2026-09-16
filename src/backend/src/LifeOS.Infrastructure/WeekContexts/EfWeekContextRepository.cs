using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.WeekContexts;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.WeekContexts;

public sealed class EfWeekContextRepository(LifeOSDbContext dbContext) : IWeekContextRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<WeekContext> GetForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var weekContext = await _dbContext.WeekContexts
            .FirstOrDefaultAsync(context => context.HouseholdId == householdId, cancellationToken);

        if (weekContext is not null)
        {
            return weekContext;
        }

        weekContext = WeekContext.CreateDefault(householdId, DateOnly.FromDateTime(DateTime.UtcNow));
        await _dbContext.WeekContexts.AddAsync(weekContext, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return weekContext;
    }

    public async Task SaveAsync(WeekContext weekContext, CancellationToken cancellationToken = default)
    {
        _dbContext.WeekContexts.Update(weekContext);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
