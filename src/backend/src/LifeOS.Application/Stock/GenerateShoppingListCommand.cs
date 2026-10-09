using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.WeekPlanning;

namespace LifeOS.Application.Stock;

/// <summary>Historical lists remain readable; generation is not part of the manual MVP.</summary>
public sealed class GenerateShoppingListCommand(IWeekRepository weekRepository)
{
    public async Task<List<ShoppingListItemDto>> ExecuteAsync(Guid householdId, Guid weekId, CancellationToken cancellationToken)
    {
        if (await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken) is null)
            throw new ArgumentException("Week not found or does not belong to this household.", nameof(weekId));
        // Never clear historical rows or approximate ingredient identities/units.
        throw new ArgumentException("Les courses automatiques sont hors MVP manuel. Les listes historiques restent consultables.");
    }
}
