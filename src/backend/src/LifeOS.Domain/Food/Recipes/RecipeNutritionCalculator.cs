using LifeOS.Domain.Food.Foods;

namespace LifeOS.Domain.Food.Recipes;

/// <summary>One nutrient per serving. <see cref="IsPartial"/>: only known values were added.</summary>
public sealed record NutrientPerServing(string Nutrient, string Unit, double Value, bool IsPartial);

/// <summary>Nutrition per serving of a recipe; <see cref="IncompleteFoodIds"/> lists the foods that made it partial.</summary>
public sealed record RecipeNutrition(IReadOnlyList<NutrientPerServing> PerServing, IReadOnlyList<Guid> IncompleteFoodIds)
{
    public bool IsComplete => IncompleteFoodIds.Count == 0;
}

/// <summary>
/// Nutrition of a recipe computed on read, never stored: ingredient quantity × value per
/// nutrition base / yield. An unknown value, an unknown base or a base of another dimension
/// (g against "1 pièce") leaves the nutrient partial: no density or piece weight is invented.
/// Same rule as the app's recipe sheet.
/// </summary>
public static class RecipeNutritionCalculator
{
    private static readonly (string Name, string Unit, Func<Nutrition, double?> Read)[] Nutrients =
    [
        ("Calories", "kcal", n => n.CaloriesPerUnit),
        ("Protéines", "g", n => n.ProteinsPerUnit),
        ("Glucides", "g", n => n.CarbsPerUnit),
        ("Lipides", "g", n => n.FatsPerUnit),
        ("Fibres", "g", n => n.FibersPerUnit),
    ];

    public static RecipeNutrition Compute(Recipe recipe, IReadOnlyDictionary<Guid, FoodItem> foods)
    {
        var incomplete = new List<Guid>();
        var perServing = Nutrients.Select(nutrient =>
        {
            var total = 0d;
            var partial = false;
            foreach (var ingredient in recipe.Ingredients)
            {
                var food = foods.GetValueOrDefault(ingredient.FoodItemId);
                var value = food?.Nutrition is { } nutrition ? nutrient.Read(nutrition) : null;
                var factor = food is null ? null : Factor(ingredient.Quantity, ingredient.Unit, food.ReferenceUnit);
                if (value is null || factor is null)
                {
                    partial = true;
                    if (!incomplete.Contains(ingredient.FoodItemId)) incomplete.Add(ingredient.FoodItemId);
                }
                else total += value.Value * factor.Value;
            }
            return new NutrientPerServing(nutrient.Name, nutrient.Unit, total / recipe.Servings, partial);
        }).ToList();
        return new RecipeNutrition(perServing, incomplete);
    }

    /// <summary>How many nutrition bases a quantity holds (250 g of a food per "100 g" is 2.5); null across dimensions.</summary>
    public static double? Factor(decimal quantity, string unit, string referenceUnit)
    {
        var amount = Measures.ToBase(quantity, unit);
        var nutritionBase = Measures.NutritionBase(referenceUnit);
        if (amount is null || nutritionBase is null || amount.Value.Dimension != nutritionBase.Value.Dimension) return null;
        return (double)(amount.Value.Amount / nutritionBase.Value.Amount);
    }
}
