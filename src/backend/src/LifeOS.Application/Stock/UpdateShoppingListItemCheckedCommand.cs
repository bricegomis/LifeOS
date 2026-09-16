using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Stock;

public sealed class UpdateShoppingListItemCheckedCommand(IShoppingListItemRepository shoppingListItemRepository)
{
    private readonly IShoppingListItemRepository _shoppingListItemRepository = shoppingListItemRepository;

    public async Task<ShoppingListItemDto?> ExecuteAsync(
        Guid householdId,
        Guid shoppingListItemId,
        bool @checked,
        CancellationToken cancellationToken)
    {
        var shoppingListItem = await _shoppingListItemRepository.GetByIdAsync(
            householdId,
            shoppingListItemId,
            cancellationToken);

        if (shoppingListItem == null)
        {
            return null;
        }

        shoppingListItem.UpdateChecked(@checked);

        await _shoppingListItemRepository.UpdateAsync(shoppingListItem, cancellationToken);

        return new ShoppingListItemDto(
            shoppingListItem.Id,
            shoppingListItem.HouseholdId,
            shoppingListItem.WeekId,
            shoppingListItem.GroceryItemId,
            shoppingListItem.QuantityNeeded,
            shoppingListItem.QuantityFromStock,
            shoppingListItem.Checked);
    }
}
