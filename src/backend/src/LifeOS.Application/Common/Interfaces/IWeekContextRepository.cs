using LifeOS.Domain.WeekContexts;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for the per-owner <see cref="WeekContext"/> aggregate.
/// </summary>
public interface IWeekContextRepository
{
    Task<WeekContext> GetForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    Task SaveAsync(WeekContext weekContext, CancellationToken cancellationToken = default);
}
