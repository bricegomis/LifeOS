using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Stock;

public sealed class UpdateStockItemCommand(IStockItemRepository stockItemRepository)
{
    private readonly IStockItemRepository _stockItemRepository = stockItemRepository;

    public async Task<StockItemDto?> ExecuteAsync(
        Guid householdId,
        Guid stockItemId,
        decimal quantity,
        string unit,
        CancellationToken cancellationToken)
    {
        var stockItem = await _stockItemRepository.GetByIdAsync(householdId, stockItemId, cancellationToken);

        if (stockItem == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            stockItem.UpdateUnit(unit);
        }

        stockItem.UpdateQuantity(quantity);

        await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);

        return new StockItemDto(
            stockItem.Id,
            stockItem.HouseholdId,
            stockItem.GroceryItemId,
            stockItem.Quantity,
            stockItem.Unit);
    }
}
