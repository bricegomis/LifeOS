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
        var from = Normalize(Unit);
        var to = Normalize(ReferenceUnit);
        if (from == to && from.Length > 0) return (double)Quantity;
        var source = Scale(from);
        var reference = Scale(to);
        return source is { } s && reference is { } r && s.Dimension == r.Dimension
            ? (double)Quantity * s.Amount / r.Amount : null;
    }

    private static string Normalize(string unit) => unit.Trim().ToLowerInvariant().Replace(" ", "")
        .Replace("pièce", "piece").Replace("pièces", "piece");

    private static (string Dimension, double Amount)? Scale(string unit) => unit switch
    {
        "g" => ("mass", 1), "100g" => ("mass", 100), "kg" or "kilogram" => ("mass", 1000),
        "ml" => ("volume", 1), "100ml" => ("volume", 100), "l" or "liter" => ("volume", 1000),
        "piece" or "1piece" or "unit" => ("count", 1),
        _ => null,
    };
}
