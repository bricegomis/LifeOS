using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Stock;

namespace LifeOS.Application.Stock;

public sealed class CreateStockItemCommand(IStockItemRepository stockItemRepository)
{
    private readonly IStockItemRepository _stockItemRepository = stockItemRepository;

    public async Task<StockItemDto> ExecuteAsync(
        Guid householdId,
        Guid groceryItemId,
        decimal quantity,
        string unit,
        CancellationToken cancellationToken)
    {
        var stockItem = StockItem.Create(householdId, groceryItemId, quantity, unit);

        await _stockItemRepository.AddAsync(stockItem, cancellationToken);

        return new StockItemDto(
            stockItem.Id,
            stockItem.HouseholdId,
            stockItem.GroceryItemId,
            stockItem.Quantity,
            stockItem.Unit);
    }
}
