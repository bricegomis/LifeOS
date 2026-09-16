using LifeOS.Domain.Stores;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="Store"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IStoreRepository
{
    Task<IReadOnlyList<Store>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task<Store?> GetByIdAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken = default);

    Task AddAsync(Store store, CancellationToken cancellationToken = default);

    Task UpdateAsync(Store store, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken = default);
}
