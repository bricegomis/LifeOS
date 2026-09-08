using System.Collections.Concurrent;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Stores;

namespace LifeOS.Infrastructure.Stores;

/// <summary>
/// Temporary in-memory implementation of <see cref="IStoreRepository"/>, seeded with sample data.
/// This is the first, simplest persistence port; it is expected to be replaced by a real database
/// (e.g. PostgreSQL via EF Core) once the stores feature grows beyond a read-only list.
/// </summary>
public sealed class InMemoryStoreRepository : IStoreRepository
{
    private readonly ConcurrentDictionary<Guid, List<Store>> _storesByOwner = new();

    public Task<IReadOnlyList<Store>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var stores = _storesByOwner.GetOrAdd(ownerId, CreateSeedStores);

        IReadOnlyList<Store> result = stores.ToList();
        return Task.FromResult(result);
    }

    private static List<Store> CreateSeedStores(Guid ownerId)
    {
        return
        [
            Store.Create(ownerId, "Marché du coin", "12 rue des Fleurs", isOrganic: true, isLocal: true),
            Store.Create(ownerId, "Supermarché Central", "45 avenue de la République", isOrganic: false, isLocal: false),
        ];
    }
}
