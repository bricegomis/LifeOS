using LifeOS.Domain.Stores;

namespace LifeOS.Application.Stores;

internal static class StoreMapper
{
    public static StoreDto ToDto(Store store)
    {
        return new StoreDto(
            store.Id,
            store.Name,
            store.Address,
            store.IsOrganic,
            store.IsLocal,
            store.CreatedAt,
            store.UpdatedAt);
    }
}
