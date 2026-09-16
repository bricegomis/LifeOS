using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// A meal planned for a specific day (breakfast, lunch, dinner, snack).
/// Can reference either a ComposedMeal or a Recipe directly.
/// Invariant: exactly one of composed_meal_id / recipe_id is set.
/// </summary>
public sealed class PlannedMeal : Entity
{
    public Guid DayPlanId { get; private set; }
    public string MealType { get; private set; } // breakfast, lunch, dinner, snack
    public string Status { get; private set; } // planned, consumed, replaced, skipped
    public Guid? ComposedMealId { get; private set; }
    public Guid? RecipeId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation property for EF Core
    public List<PlannedMealPart> Parts { get; private set; } = [];

    private PlannedMeal(
        Guid id,
        Guid dayPlanId,
        string mealType,
        string status,
        Guid? composedMealId,
        Guid? recipeId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        DayPlanId = dayPlanId;
        MealType = mealType;
        Status = status;
        ComposedMealId = composedMealId;
        RecipeId = recipeId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static PlannedMeal Create(
        Guid dayPlanId,
        string mealType,
        Guid? composedMealId = null,
        Guid? recipeId = null,
        string status = "planned",
        DateTimeOffset? now = null)
    {
        if (dayPlanId == Guid.Empty)
        {
            throw new ArgumentException("Day plan ID is required.", nameof(dayPlanId));
        }

        if (string.IsNullOrWhiteSpace(mealType))
        {
            throw new ArgumentException("Meal type is required.", nameof(mealType));
        }

        // Invariant: exactly one of composed_meal_id / recipe_id must be set
        if ((composedMealId.HasValue && composedMealId.Value != Guid.Empty && recipeId.HasValue && recipeId.Value != Guid.Empty) ||
            (!composedMealId.HasValue && !recipeId.HasValue) ||
            (composedMealId == Guid.Empty && recipeId == Guid.Empty))
        {
            throw new ArgumentException(
                "A planned meal must reference exactly one of composed_meal_id or recipe_id.",
                nameof(composedMealId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required.", nameof(status));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new PlannedMeal(
            Guid.NewGuid(),
            dayPlanId,
            mealType.ToLowerInvariant(),
            status.ToLowerInvariant(),
            composedMealId,
            recipeId,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Updates the status of the planned meal.
    /// </summary>
    public void UpdateStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required.", nameof(status));
        }

        Status = status.ToLowerInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Replaces the meal reference (either composed meal or recipe).
    /// </summary>
    public void ReplaceMeal(Guid? composedMealId = null, Guid? recipeId = null)
    {
        if ((composedMealId.HasValue && composedMealId.Value != Guid.Empty && recipeId.HasValue && recipeId.Value != Guid.Empty) ||
            (!composedMealId.HasValue && !recipeId.HasValue) ||
            (composedMealId == Guid.Empty && recipeId == Guid.Empty))
        {
            throw new ArgumentException(
                "A planned meal must reference exactly one of composed_meal_id or recipe_id.",
                nameof(composedMealId));
        }

        ComposedMealId = composedMealId;
        RecipeId = recipeId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rehydrates a <see cref="PlannedMeal"/> from persisted state.
    /// </summary>
    public static PlannedMeal Rehydrate(
        Guid id,
        Guid dayPlanId,
        string mealType,
        string status,
        Guid? composedMealId,
        Guid? recipeId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new PlannedMeal(id, dayPlanId, mealType, status, composedMealId, recipeId, createdAt, updatedAt);
    }

}
