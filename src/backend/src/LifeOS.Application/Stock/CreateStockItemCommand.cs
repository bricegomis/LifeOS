using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Stock;

namespace LifeOS.Application.Stock;

public sealed class CreateStockItemCommand(IStockItemRepository stockItemRepository, IArticleRepository productRepository)
{
    private readonly IStockItemRepository _stockItemRepository = stockItemRepository;

    public async Task<StockItemDto> ExecuteAsync(
        Guid householdId,
        Guid groceryItemId,
        decimal quantity,
        string unit,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(householdId, groceryItemId, cancellationToken);
        if (product is null || product.IsArchived) throw new ArgumentException("Produit indisponible dans votre foyer.");
        var stockItem = StockItem.Create(householdId, product.Id, quantity, unit);

        await _stockItemRepository.AddAsync(stockItem, cancellationToken);

        return new StockItemDto(
            stockItem.Id,
            stockItem.HouseholdId,
            stockItem.GroceryItemId,
            stockItem.Quantity,
            stockItem.Unit);
    }
}
