using LifeOS.Domain.Common;
using LifeOS.Domain.FoodItems;

namespace LifeOS.Domain.WeekPlanning;

public sealed class MealFoodLine : Entity
{
    public Guid PlannedMealId { get; private set; }
    public Guid? FoodItemId { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "";
    public string ReferenceUnit { get; private set; } = "";
    public double? Calories { get; private set; }
    public double? Protein { get; private set; }
    public double? Carbs { get; private set; }
    public double? Fat { get; private set; }
    private MealFoodLine() { }

    public static MealFoodLine Snapshot(Guid mealId, Guid? foodId, string name, decimal quantity,
        string unit, string referenceUnit, Nutrition? nutrition)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit) || unit.Length > 50)
            throw new ArgumentException("Nom et unité obligatoires.");
        var line = new MealFoodLine
        {
            Id = Guid.NewGuid(), PlannedMealId = mealId, FoodItemId = foodId, Name = name,
            Unit = unit.Trim(), ReferenceUnit = referenceUnit, Calories = nutrition?.CaloriesPerUnit,
            Protein = nutrition?.ProteinsPerUnit, Carbs = nutrition?.CarbsPerUnit, Fat = nutrition?.FatsPerUnit,
        };
        line.UpdateQuantity(quantity);
        return line;
    }

    public void UpdateQuantity(decimal quantity)
    {
        var rounded = decimal.Round(quantity, 12);
        if (rounded <= 0 || quantity > 1000000)
            throw new ArgumentException("Quantité positive (précision douze décimales), maximum 1000000.");
        Quantity = rounded;
    }

    public double? NutritionFactor()
    {
        var converted = QuantityConversion.Convert(Quantity, Unit, ReferenceUnit);
        return converted is { } value ? (double)value : null;
    }
}
