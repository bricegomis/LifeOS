using LifeOS.Domain.WeekContexts;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for the per-household <see cref="WeekContext"/> aggregate.
/// </summary>
public interface IWeekContextRepository
{
    Task<WeekContext> GetForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task SaveAsync(WeekContext weekContext, CancellationToken cancellationToken = default);
}
