using LifeOS.Domain.Stock;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="StockItem"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IStockItemRepository
{
    Task<IReadOnlyList<StockItem>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task<StockItem?> GetByIdAsync(Guid householdId, Guid stockItemId, CancellationToken cancellationToken = default);

    Task AddAsync(StockItem stockItem, CancellationToken cancellationToken = default);

    Task UpdateAsync(StockItem stockItem, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid householdId, Guid stockItemId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persistence port for <see cref="ShoppingListItem"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IShoppingListItemRepository
{
    Task<IReadOnlyList<ShoppingListItem>> GetAllForHouseholdAsync(Guid householdId, Guid? weekId = null, CancellationToken cancellationToken = default);

    Task<ShoppingListItem?> GetByIdAsync(Guid householdId, Guid shoppingListItemId, CancellationToken cancellationToken = default);

    Task AddAsync(ShoppingListItem shoppingListItem, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<ShoppingListItem> shoppingListItems, CancellationToken cancellationToken = default);

    Task UpdateAsync(ShoppingListItem shoppingListItem, CancellationToken cancellationToken = default);

    Task RemoveAsync(ShoppingListItem shoppingListItem, CancellationToken cancellationToken = default);

    Task RemoveRangeAsync(IEnumerable<ShoppingListItem> shoppingListItems, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShoppingListItem>> GetAllForWeekAsync(Guid householdId, Guid weekId, CancellationToken cancellationToken = default);
}
