using LifeOS.Domain.FoodItems;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.FoodItems;

/// <summary>
/// EF Core implementation of the food items repository (Jalon 3).
/// </summary>
public sealed class EfFoodItemRepository(LifeOSDbContext dbContext) : IFoodItemRepository
{
    public async Task<IEnumerable<FoodItem>> GetByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        return await dbContext.FoodItems
            .Where(f => f.HouseholdId == householdId)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<FoodItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.FoodItems.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<FoodItem?> FindByOffBarcodeAsync(Guid householdId, string barcode, CancellationToken cancellationToken = default)
    {
        return await dbContext.FoodItems
            .FirstOrDefaultAsync(f => f.HouseholdId == householdId && f.OffBarcode == barcode, cancellationToken);
    }

    public async Task AddAsync(FoodItem foodItem, CancellationToken cancellationToken = default)
    {
        await dbContext.FoodItems.AddAsync(foodItem, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FoodItem foodItem, CancellationToken cancellationToken = default)
    {
        dbContext.FoodItems.Update(foodItem);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var foodItem = await dbContext.FoodItems.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (foodItem != null)
        {
            dbContext.FoodItems.Remove(foodItem);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
