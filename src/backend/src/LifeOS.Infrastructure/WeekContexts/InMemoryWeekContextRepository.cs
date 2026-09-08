using System.Collections.Concurrent;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.WeekContexts;

namespace LifeOS.Infrastructure.WeekContexts;

/// <summary>
/// Temporary in-memory implementation of <see cref="IWeekContextRepository"/>, lazily creating a
/// default context per owner. This is the first, simplest persistence port; it is expected to be
/// replaced by a real database once the feature grows beyond a single per-owner record.
/// </summary>
public sealed class InMemoryWeekContextRepository : IWeekContextRepository
{
    private readonly ConcurrentDictionary<Guid, WeekContext> _contextsByOwner = new();

    public Task<WeekContext> GetForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var context = _contextsByOwner.GetOrAdd(
            ownerId,
            id => WeekContext.CreateDefault(id, DateOnly.FromDateTime(DateTime.UtcNow)));

        return Task.FromResult(context);
    }

    public Task SaveAsync(WeekContext weekContext, CancellationToken cancellationToken = default)
    {
        _contextsByOwner[weekContext.OwnerId] = weekContext;

        return Task.CompletedTask;
    }
}
