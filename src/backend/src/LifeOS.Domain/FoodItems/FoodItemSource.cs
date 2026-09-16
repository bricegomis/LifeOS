namespace LifeOS.Domain.FoodItems;

/// <summary>
/// Source of a food item: either from Open Food Facts (cached locally) or created manually.
/// </summary>
public enum FoodItemSource
{
    /// <summary>
    /// Item is from Open Food Facts (cached locally).
    /// </summary>
    OpenFoodFacts,

    /// <summary>
    /// Item was created manually by a household.
    /// </summary>
    Manual,
}
