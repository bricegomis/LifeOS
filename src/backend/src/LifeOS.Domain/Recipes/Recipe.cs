using LifeOS.Domain.Common;

namespace LifeOS.Domain.Recipes;

/// <summary>
/// A recipe is a collection of ingredients with instructions and metadata.
/// Aggregate root of the Recipes bounded context.
/// </summary>
public sealed class Recipe : Entity
{
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; }
    public int Servings { get; private set; }
    public int DurationMinutes { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public Dictionary<string, object>? Metadata { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation property for EF Core
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Backing field for EF Core")]
    private List<RecipeIngredient> _ingredients = [];
    public IReadOnlyList<RecipeIngredient> Ingredients => _ingredients.AsReadOnly();

    private Recipe(
        Guid id,
        Guid householdId,
        string name,
        int servings,
        int durationMinutes,
        List<string> tags,
        Dictionary<string, object>? metadata,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        Name = name;
        Servings = servings;
        DurationMinutes = durationMinutes;
        Tags = tags;
        Metadata = metadata;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Recipe Create(
        Guid householdId,
        string name,
        int servings,
        int durationMinutes,
        List<string>? tags = null,
        Dictionary<string, object>? metadata = null,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A recipe must belong to a household.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Recipe name is required.", nameof(name));
        }

        if (servings <= 0)
        {
            throw new ArgumentException("Servings must be greater than 0.", nameof(servings));
        }

        if (durationMinutes < 0)
        {
            throw new ArgumentException("Duration cannot be negative.", nameof(durationMinutes));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new Recipe(
            Guid.NewGuid(),
            householdId,
            name.Trim(),
            servings,
            durationMinutes,
            tags ?? [],
            metadata,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Adds an ingredient to the recipe.
    /// </summary>
    public void AddIngredient(Guid foodItemId, decimal quantity, string unit)
    {
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

        var ingredient = RecipeIngredient.Create(Id, foodItemId, quantity, unit);
        _ingredients.Add(ingredient);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates recipe metadata.
    /// </summary>
    public void Update(
        string? name = null,
        int? servings = null,
        int? durationMinutes = null,
        List<string>? tags = null,
        Dictionary<string, object>? metadata = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (servings.HasValue && servings.Value > 0)
        {
            Servings = servings.Value;
        }

        if (durationMinutes.HasValue && durationMinutes.Value >= 0)
        {
            DurationMinutes = durationMinutes.Value;
        }

        if (tags != null)
        {
            Tags = tags;
        }

        if (metadata != null)
        {
            Metadata = metadata;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rehydrates a <see cref="Recipe"/> from persisted state.
    /// </summary>
    public static Recipe Rehydrate(
        Guid id,
        Guid householdId,
        string name,
        int servings,
        int durationMinutes,
        List<string> tags,
        Dictionary<string, object>? metadata,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Recipe(id, householdId, name, servings, durationMinutes, tags, metadata, createdAt, updatedAt);
    }

    /// <summary>
    /// Called by EF Core to load ingredients.
    /// </summary>
    internal void SetIngredients(List<RecipeIngredient> ingredients)
    {
        _ingredients.Clear();
        _ingredients.AddRange(ingredients);
    }
}
