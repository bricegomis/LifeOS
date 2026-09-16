using LifeOS.Domain.Common;

namespace LifeOS.Domain.ComposedMeals;

/// <summary>
/// A part of a composed meal, referencing a recipe.
/// </summary>
public sealed class ComposedMealPart : Entity
{
    public Guid ComposedMealId { get; private set; }
    public Guid RecipeId { get; private set; }
    public decimal QuantityFactor { get; private set; }

    private ComposedMealPart(
        Guid id,
        Guid composedMealId,
        Guid recipeId,
        decimal quantityFactor)
        : base(id)
    {
        ComposedMealId = composedMealId;
        RecipeId = recipeId;
        QuantityFactor = quantityFactor;
    }

    public static ComposedMealPart Create(
        Guid composedMealId,
        Guid recipeId,
        decimal quantityFactor)
    {
        if (composedMealId == Guid.Empty)
        {
            throw new ArgumentException("Composed meal ID is required.", nameof(composedMealId));
        }

        if (recipeId == Guid.Empty)
        {
            throw new ArgumentException("Recipe ID is required.", nameof(recipeId));
        }

        if (quantityFactor <= 0)
        {
            throw new ArgumentException("Quantity factor must be greater than 0.", nameof(quantityFactor));
        }

        return new ComposedMealPart(Guid.NewGuid(), composedMealId, recipeId, quantityFactor);
    }

    /// <summary>
    /// Rehydrates a <see cref="ComposedMealPart"/> from persisted state.
    /// </summary>
    public static ComposedMealPart Rehydrate(
        Guid id,
        Guid composedMealId,
        Guid recipeId,
        decimal quantityFactor)
    {
        return new ComposedMealPart(id, composedMealId, recipeId, quantityFactor);
    }

    /// <summary>
    /// Updates the quantity factor.
    /// </summary>
    public void Update(decimal quantityFactor)
    {
        if (quantityFactor <= 0)
        {
            throw new ArgumentException("Quantity factor must be greater than 0.", nameof(quantityFactor));
        }

        QuantityFactor = quantityFactor;
    }
}
