using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Stores;

/// <summary>
/// Use case: list every grocery store belonging to the current user.
/// </summary>
public sealed class GetStoresQuery(IStoreRepository storeRepository)
{
    private readonly IStoreRepository _storeRepository = storeRepository;

    public async Task<IReadOnlyList<StoreDto>> ExecuteAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var stores = await _storeRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

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

/// <summary>
/// Use case: get one grocery store belonging to the current household.
/// </summary>
public sealed class GetStoreQuery(IStoreRepository storeRepository)
{
    private readonly IStoreRepository _storeRepository = storeRepository;

    public async Task<StoreDto?> ExecuteAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(householdId, storeId, cancellationToken);

        return store is null ? null : StoreMapper.ToDto(store);
    }
}
