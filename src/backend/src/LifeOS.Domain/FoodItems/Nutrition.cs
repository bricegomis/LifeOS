namespace LifeOS.Domain.FoodItems;

/// <summary>
/// Nutritional information per reference unit of a food item.
/// </summary>
public sealed class Nutrition
{
    public double? CaloriesPerUnit { get; init; }
    public double? ProteinsPerUnit { get; init; }
    public double? CarbsPerUnit { get; init; }
    public double? FatsPerUnit { get; init; }

    private Nutrition() { }

    public Nutrition(double? caloriesPerUnit, double? proteinsPerUnit, double? carbsPerUnit, double? fatsPerUnit)
    {
        CaloriesPerUnit = caloriesPerUnit;
        ProteinsPerUnit = proteinsPerUnit;
        CarbsPerUnit = carbsPerUnit;
        FatsPerUnit = fatsPerUnit;
    }
}
