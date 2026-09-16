using LifeOS.Domain.Common;

namespace LifeOS.Domain.FoodItems;

/// <summary>
/// A food item representing a product from Open Food Facts or manually created.
/// Aggregate root of the FoodItems bounded context.
/// </summary>
public sealed class FoodItem : Entity
{
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; }
    public string ReferenceUnit { get; private set; }
    public Nutrition? Nutrition { get; private set; }
    public FoodItemSource Source { get; private set; }
    public string? OffBarcode { get; private set; }
    public string? OffPayload { get; private set; }
    public Guid? IsCorrectionOf { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Parameterless constructor for EF Core
    private FoodItem() : base(Guid.Empty)
    {
    }

    private FoodItem(
        Guid id,
        Guid householdId,
        string name,
        string referenceUnit,
        Nutrition? nutrition,
        FoodItemSource source,
        string? offBarcode,
        string? offPayload,
        Guid? isCorrectionOf,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        Name = name;
        ReferenceUnit = referenceUnit;
        Nutrition = nutrition;
        Source = source;
        OffBarcode = offBarcode;
        OffPayload = offPayload;
        IsCorrectionOf = isCorrectionOf;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a manually created food item.
    /// </summary>
    public static FoodItem CreateManual(
        Guid householdId,
        string name,
        string referenceUnit,
        Nutrition? nutrition = null,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A food item must belong to a household.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Food item name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(referenceUnit))
        {
            throw new ArgumentException("Reference unit is required.", nameof(referenceUnit));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new FoodItem(
            Guid.NewGuid(),
            householdId,
            name.Trim(),
            referenceUnit.Trim(),
            nutrition,
            FoodItemSource.Manual,
            null,
            null,
            null,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Creates a food item from Open Food Facts (cached locally).
    /// </summary>
    public static FoodItem CreateFromOpenFoodFacts(
        Guid householdId,
        string name,
        string referenceUnit,
        Nutrition? nutrition,
        string? barcode,
        string offPayload,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A food item must belong to a household.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Food item name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(referenceUnit))
        {
            throw new ArgumentException("Reference unit is required.", nameof(referenceUnit));
        }

        if (string.IsNullOrWhiteSpace(offPayload))
        {
            throw new ArgumentException("Open Food Facts payload is required.", nameof(offPayload));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new FoodItem(
            Guid.NewGuid(),
            householdId,
            name.Trim(),
            referenceUnit.Trim(),
            nutrition,
            FoodItemSource.OpenFoodFacts,
            barcode,
            offPayload,
            null,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Creates a manual correction of an existing food item.
    /// </summary>
    public static FoodItem CreateCorrection(
        Guid householdId,
        string name,
        string referenceUnit,
        Nutrition? nutrition,
        Guid originalFoodItemId,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A food item must belong to a household.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Food item name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(referenceUnit))
        {
            throw new ArgumentException("Reference unit is required.", nameof(referenceUnit));
        }

        if (originalFoodItemId == Guid.Empty)
        {
            throw new ArgumentException("Original food item ID is required.", nameof(originalFoodItemId));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new FoodItem(
            Guid.NewGuid(),
            householdId,
            name.Trim(),
            referenceUnit.Trim(),
            nutrition,
            FoodItemSource.Manual,
            null,
            null,
            originalFoodItemId,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Rehydrates a <see cref="FoodItem"/> from persisted state.
    /// </summary>
    public static FoodItem Rehydrate(
        Guid id,
        Guid householdId,
        string name,
        string referenceUnit,
        Nutrition? nutrition,
        FoodItemSource source,
        string? offBarcode,
        string? offPayload,
        Guid? isCorrectionOf,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new FoodItem(id, householdId, name, referenceUnit, nutrition, source, offBarcode, offPayload, isCorrectionOf, createdAt, updatedAt);
    }

    /// <summary>
    /// Updates food item details.
    /// </summary>
    public void UpdateDetails(
        string name,
        string referenceUnit,
        Nutrition? nutrition,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Food item name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(referenceUnit))
        {
            throw new ArgumentException("Reference unit is required.", nameof(referenceUnit));
        }

        Name = name.Trim();
        ReferenceUnit = referenceUnit.Trim();
        Nutrition = nutrition;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }
}
