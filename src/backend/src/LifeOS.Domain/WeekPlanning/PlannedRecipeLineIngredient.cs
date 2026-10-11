using LifeOS.Domain.Common;
using LifeOS.Domain.FoodItems;

namespace LifeOS.Domain.WeekPlanning;

public sealed class PlannedRecipeLineIngredient : Entity
{
    public Guid PlannedRecipeLineId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "";
    public string ReferenceUnit { get; private set; } = "";
    public double? Calories { get; private set; }
    public double? Protein { get; private set; }
    public double? Carbs { get; private set; }
    public double? Fat { get; private set; }

    private PlannedRecipeLineIngredient() { }

    public static PlannedRecipeLineIngredient Snapshot(Guid lineId, Guid householdId, Guid foodId,
        string name, decimal quantity, string unit, string referenceUnit, Nutrition? nutrition)
    {
        if (lineId == Guid.Empty || householdId == Guid.Empty || foodId == Guid.Empty)
            throw new ArgumentException("La ligne, le foyer et l'aliment sont obligatoires.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 50)
            throw new ArgumentException("Nom d'aliment et unité obligatoires.");
        var rounded = decimal.Round(quantity, 12);
        if (rounded <= 0 || quantity > 1000000 || quantity != rounded)
            throw new ArgumentException("La quantité doit être positive, précise à douze décimales au plus.");
        return new PlannedRecipeLineIngredient
        {
            Id = Guid.NewGuid(), PlannedRecipeLineId = lineId, HouseholdId = householdId, FoodItemId = foodId,
            Name = name.Trim(), Quantity = rounded, Unit = unit.Trim(), ReferenceUnit = referenceUnit,
            Calories = nutrition?.CaloriesPerUnit, Protein = nutrition?.ProteinsPerUnit,
            Carbs = nutrition?.CarbsPerUnit, Fat = nutrition?.FatsPerUnit,
        };
    }

    public double? NutritionFactor()
    {
        var converted = QuantityConversion.Convert(Quantity, Unit, ReferenceUnit);
        return converted is { } value ? (double)value : null;
    }
}
