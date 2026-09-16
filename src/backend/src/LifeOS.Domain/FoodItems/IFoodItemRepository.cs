namespace LifeOS.Domain.FoodItems;

/// <summary>
/// Repository contract for food items.
/// </summary>
public interface IFoodItemRepository
{
    /// <summary>
    /// Gets all food items for a household.
    /// </summary>
    Task<IEnumerable<FoodItem>> GetByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific food item by ID.
    /// </summary>
    Task<FoodItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a food item by OFF barcode for a household.
    /// </summary>
    Task<FoodItem?> FindByOffBarcodeAsync(Guid householdId, string barcode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new food item.
    /// </summary>
    Task AddAsync(FoodItem foodItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing food item.
    /// </summary>
    Task UpdateAsync(FoodItem foodItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a food item.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
