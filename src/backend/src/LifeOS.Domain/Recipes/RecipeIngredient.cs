using LifeOS.Domain.Common;

namespace LifeOS.Domain.Recipes;

/// <summary>
/// An ingredient line in a recipe.
/// </summary>
public sealed class RecipeIngredient : Entity
{
    public Guid RecipeId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; }

    private RecipeIngredient(
        Guid id,
        Guid recipeId,
        Guid foodItemId,
        decimal quantity,
        string unit)
        : base(id)
    {
        RecipeId = recipeId;
        FoodItemId = foodItemId;
        Quantity = quantity;
        Unit = unit;
    }

    public static RecipeIngredient Create(
        Guid recipeId,
        Guid foodItemId,
        decimal quantity,
        string unit)
    {
        if (recipeId == Guid.Empty)
        {
            throw new ArgumentException("Recipe ID is required.", nameof(recipeId));
        }

        if (foodItemId == Guid.Empty)
        {
            throw new ArgumentException("Food item ID is required.", nameof(foodItemId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.", nameof(quantity));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit is required.", nameof(unit));
        }

        return new RecipeIngredient(Guid.NewGuid(), recipeId, foodItemId, quantity, unit.Trim());
    }

    /// <summary>
    /// Rehydrates a <see cref="RecipeIngredient"/> from persisted state.
    /// </summary>
    public static RecipeIngredient Rehydrate(
        Guid id,
        Guid recipeId,
        Guid foodItemId,
        decimal quantity,
        string unit)
    {
        return new RecipeIngredient(id, recipeId, foodItemId, quantity, unit);
    }

    /// <summary>
    /// Updates ingredient quantity and unit.
    /// </summary>
    public void Update(decimal quantity, string unit)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.", nameof(quantity));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit is required.", nameof(unit));
        }

        Quantity = quantity;
        Unit = unit.Trim();
    }
}
