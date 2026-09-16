using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Stores;

namespace LifeOS.Application.Stores;

/// <summary>
/// Use case: create a grocery store in the current household.
/// </summary>
public sealed class CreateStoreCommand(IStoreRepository storeRepository)
{
    private readonly IStoreRepository _storeRepository = storeRepository;

    public async Task<StoreDto> ExecuteAsync(
        Guid householdId,
        string name,
        string address,
        bool isOrganic,
        bool isLocal,
        CancellationToken cancellationToken = default)
    {
        var store = Store.Create(householdId, name, address, isOrganic, isLocal);
        await _storeRepository.AddAsync(store, cancellationToken);

        return StoreMapper.ToDto(store);
    }
}

/// <summary>
/// Use case: update a grocery store in the current household.
/// </summary>
public sealed class UpdateStoreCommand(IStoreRepository storeRepository)
{
    private readonly IStoreRepository _storeRepository = storeRepository;

    public async Task<StoreDto?> ExecuteAsync(
        Guid householdId,
        Guid storeId,
        string name,
        string address,
        bool isOrganic,
        bool isLocal,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(householdId, storeId, cancellationToken);

        if (store is null)
        {
            return null;
        }

        store.UpdateDetails(name, address, isOrganic, isLocal);
        await _storeRepository.UpdateAsync(store, cancellationToken);

        return StoreMapper.ToDto(store);
    }
}

/// <summary>
/// Use case: delete a grocery store in the current household.
/// </summary>
public sealed class DeleteStoreCommand(IStoreRepository storeRepository)
{
    private readonly IStoreRepository _storeRepository = storeRepository;

    public Task<bool> ExecuteAsync(Guid householdId, Guid storeId, CancellationToken cancellationToken = default)
        => _storeRepository.DeleteAsync(householdId, storeId, cancellationToken);
}
