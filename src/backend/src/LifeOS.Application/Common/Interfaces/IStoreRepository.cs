using LifeOS.Domain.Stores;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="Store"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IStoreRepository
{
    Task<IReadOnlyList<Store>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
}
