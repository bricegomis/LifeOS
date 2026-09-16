using LifeOS.Domain.Common;

namespace LifeOS.Domain.ComposedMeals;

/// <summary>
/// A composed meal is an assembly of recipes/components forming a plannable meal unit.
/// Aggregate root of the ComposedMeals bounded context.
/// </summary>
public sealed class ComposedMeal : Entity
{
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation property for EF Core
    private readonly List<ComposedMealPart> _parts = [];
    public IReadOnlyList<ComposedMealPart> Parts => _parts.AsReadOnly();

    private ComposedMeal(
        Guid id,
        Guid householdId,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static ComposedMeal Create(
        Guid householdId,
        string name,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A composed meal must belong to a household.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Composed meal name is required.", nameof(name));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new ComposedMeal(
            Guid.NewGuid(),
            householdId,
            name.Trim(),
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Adds a recipe part to the composed meal.
    /// </summary>
    public void AddPart(Guid recipeId, decimal quantityFactor)
    {
        if (recipeId == Guid.Empty)
        {
            throw new ArgumentException("Recipe ID is required.", nameof(recipeId));
        }

        if (quantityFactor <= 0)
        {
            throw new ArgumentException("Quantity factor must be greater than 0.", nameof(quantityFactor));
        }

        var part = ComposedMealPart.Create(Id, recipeId, quantityFactor);
        _parts.Add(part);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes a part from the composed meal.
    /// </summary>
    public void RemovePart(Guid partId)
    {
        var part = _parts.FirstOrDefault(p => p.Id == partId);
        if (part != null)
        {
            _parts.Remove(part);
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Updates the composed meal name.
    /// </summary>
    public void Update(string? name = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rehydrates a <see cref="ComposedMeal"/> from persisted state.
    /// </summary>
    public static ComposedMeal Rehydrate(
        Guid id,
        Guid householdId,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new ComposedMeal(id, householdId, name, createdAt, updatedAt);
    }

    /// <summary>
    /// Called by EF Core to load parts.
    /// </summary>
    internal void SetParts(List<ComposedMealPart> parts)
    {
        _parts.Clear();
        _parts.AddRange(parts);
    }
}
