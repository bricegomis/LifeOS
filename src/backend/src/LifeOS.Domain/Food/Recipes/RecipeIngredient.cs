using LifeOS.Domain.Common;

namespace LifeOS.Domain.Food.Recipes;

/// <summary>A recipe line: one food, an exact quantity and its unit. No conversion is ever inferred.</summary>
public sealed class RecipeIngredient : Entity
{
    public const int MaxUnitLength = 50;

    public Guid RecipeId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "";

    private RecipeIngredient() { }

    internal static RecipeIngredient Create(Guid recipeId, Guid foodItemId, decimal quantity, string unit)
    {
        if (recipeId == Guid.Empty) throw new ArgumentException("Recette requise.");
        if (foodItemId == Guid.Empty) throw new ArgumentException("Un ingrédient référence un aliment.");
        var ingredient = new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipeId, FoodItemId = foodItemId };
        ingredient.Update(quantity, unit);
        return ingredient;
    }

    internal void Update(decimal quantity, string unit)
    {
        if (quantity < 0.000001m || quantity > 1000000 || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentException("Quantité positive requise (6 décimales maximum).");
        if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > MaxUnitLength)
            throw new ArgumentException($"Unité requise ({MaxUnitLength} caractères maximum).");
        Quantity = quantity;
        Unit = unit.Trim();
    }
}
