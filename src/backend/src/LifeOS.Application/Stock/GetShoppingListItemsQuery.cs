using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Stock;

public sealed class GetShoppingListItemsQuery(IShoppingListItemRepository shoppingListItemRepository)
{
    private readonly IShoppingListItemRepository _shoppingListItemRepository = shoppingListItemRepository;

    public async Task<List<ShoppingListItemDto>> ExecuteAsync(
        Guid householdId,
        Guid? weekId = null,
        CancellationToken cancellationToken = default)
    {
        var items = await _shoppingListItemRepository.GetAllForHouseholdAsync(householdId, weekId, cancellationToken);

        return items.Select(item => new ShoppingListItemDto(
            item.Id,
            item.HouseholdId,
            item.WeekId,
            item.GroceryItemId,
            item.QuantityNeeded,
            item.QuantityFromStock,
            item.Checked)).ToList();
    }
}

public sealed record ShoppingListItemDto(
    Guid Id,
    Guid HouseholdId,
    Guid? WeekId,
    Guid GroceryItemId,
    decimal QuantityNeeded,
    decimal QuantityFromStock,
    bool Checked);
