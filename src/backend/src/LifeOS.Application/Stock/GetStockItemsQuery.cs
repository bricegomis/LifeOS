using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Stock;

public sealed class GetStockItemsQuery(IStockItemRepository stockItemRepository)
{
    private readonly IStockItemRepository _stockItemRepository = stockItemRepository;

    public async Task<List<StockItemDto>> ExecuteAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var items = await _stockItemRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

        return items.Select(item => new StockItemDto(
            item.Id,
            item.HouseholdId,
            item.GroceryItemId,
            item.Quantity,
            item.Unit)).ToList();
    }
}

public sealed record StockItemDto(
    Guid Id,
    Guid HouseholdId,
    Guid GroceryItemId,
    decimal Quantity,
    string Unit);
