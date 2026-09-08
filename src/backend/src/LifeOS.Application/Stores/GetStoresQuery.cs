using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Stores;

/// <summary>
/// Use case: list every grocery store belonging to the current user.
/// </summary>
public sealed class GetStoresQuery(IStoreRepository storeRepository)
{
    private readonly IStoreRepository _storeRepository = storeRepository;

    public async Task<IReadOnlyList<StoreDto>> ExecuteAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var stores = await _storeRepository.GetAllForOwnerAsync(ownerId, cancellationToken);

        return stores
            .Select(store => new StoreDto(
                store.Id,
                store.Name,
                store.Address,
                store.IsOrganic,
                store.IsLocal,
                store.CreatedAt,
                store.UpdatedAt))
            .OrderBy(store => store.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
