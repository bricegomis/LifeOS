using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Stock;

public sealed class DeleteStockItemCommand(IStockItemRepository stockItemRepository)
{
    private readonly IStockItemRepository _stockItemRepository = stockItemRepository;

    public async Task<bool> ExecuteAsync(
        Guid householdId,
        Guid stockItemId,
        CancellationToken cancellationToken)
    {
        return await _stockItemRepository.DeleteAsync(householdId, stockItemId, cancellationToken);
    }
}
