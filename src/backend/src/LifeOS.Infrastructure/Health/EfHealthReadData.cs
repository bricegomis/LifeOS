using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Health;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Health;

internal sealed class EfHealthReadData(LifeOSDbContext db) : IHealthReadData
{
    public async Task<IReadOnlyList<DailyWellness>> WellnessAsync(Guid householdId, Guid ownerUserId, DateOnly from, DateOnly to,
        CancellationToken ct) =>
        await db.DailyWellness.AsNoTracking()
            .Where(w => w.HouseholdId == householdId && w.OwnerUserId == ownerUserId && w.Date >= from && w.Date <= to)
            .ToListAsync(ct);
}
