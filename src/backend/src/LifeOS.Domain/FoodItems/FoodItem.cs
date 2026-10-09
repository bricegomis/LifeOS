using LifeOS.Domain.Common;
using LifeOS.Domain.Articles;

namespace LifeOS.Domain.FoodItems;

/// <summary>
/// Canonical household product: optional nutrition and owned purchase history.
/// The historical FoodItem type name is retained for API/source compatibility.
/// </summary>
public sealed class FoodItem : Entity
{
    public Guid HouseholdId { get; private set; }
    private readonly List<GroceryPriceEntry> _priceHistory = [];
    public string Description { get; private set; } = "";
    public GroceryItemUnit Unit { get; private set; } = GroceryItemUnit.Unit;
    public bool PurchaseUnitConfirmed { get; private set; }
    public IReadOnlyList<GroceryPriceEntry> PriceHistory => _priceHistory;
    public string? LegacyPurchaseName { get; private set; }
    public string? MigrationOrigin { get; private set; }
    // Compatibility identifier, not an association to another aggregate.
    public Guid ArticleId => Id;
    public bool IsArchived { get; private set; }
    public string Name { get; private set; }
    public string ReferenceUnit { get; private set; }
    public Nutrition? Nutrition { get; private set; }
    public FoodItemSource Source { get; private set; }
    public string? OffBarcode { get; private set; }
    public string? OffPayload { get; private set; }
    public Guid? IsCorrectionOf { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdatePurchaseDetails(string description, GroceryItemUnit unit, DateTimeOffset? now = null)
    {
        if (!Enum.IsDefined(unit)) throw new ArgumentException("Unité d'achat invalide.");
        if (description.Length > 1000) throw new ArgumentException("Description limitée à 1000 caractères.");
        if (unit != Unit && _priceHistory.Count > 0)
            throw new ArgumentException("L'unité d'achat ne peut pas changer après un relevé de prix : les prix historiques utilisent cette unité.");
        Description = description.Trim();
        Unit = unit;
        PurchaseUnitConfirmed = true;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(string name, string description, GroceryItemUnit unit, DateTimeOffset? now = null)
    {
        UpdatePurchaseDetails(description, unit, now);
        UpdateDetails(name, ReferenceUnit, Nutrition, now);
    }

    public static FoodItem Create(Guid householdId, string name, string description, GroceryItemUnit unit, DateTimeOffset? now = null)
    {
        var product = CreateManual(householdId, name, "", now: now);
        product.UpdatePurchaseDetails(description, unit, now);
        return product;
    }

    public static FoodItem Rehydrate(Guid id, Guid householdId, string name, string description,
        GroceryItemUnit unit, IEnumerable<GroceryPriceEntry> priceHistory, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var product = new FoodItem(id, householdId, name, "", null, FoodItemSource.Manual, null, null, null, createdAt, updatedAt);
        product.Description = description;
        product.Unit = unit;
        product.PurchaseUnitConfirmed = true;
        product._priceHistory.AddRange(priceHistory);
        return product;
    }

    public GroceryPriceEntry AddPriceEntry(Guid storeId, decimal price, DateTimeOffset observedAt, DateTimeOffset? now = null)
    {
        if (IsArchived) throw new ArgumentException("Ce produit est archivé.");
        if (!PurchaseUnitConfirmed) throw new ArgumentException("Précisez d'abord l'unité d'achat du produit.");
        var entry = GroceryPriceEntry.Create(storeId, price, observedAt, now ?? DateTimeOffset.UtcNow);
        _priceHistory.Add(entry);
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        return entry;
    }

    public bool RemovePriceEntry(Guid id, DateTimeOffset? now = null)
    {
        var removed = _priceHistory.RemoveAll(e => e.Id == id) > 0;
        if (removed) UpdatedAt = now ?? DateTimeOffset.UtcNow;
        return removed;
    }

    public void Archive()
    {
        IsArchived = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

#pragma warning disable CS8618
    private FoodItem()
    {
        // Parameterless constructor for EF Core materialization.
        // EF will set properties via field access after construction.
        // Properties are not initialized here but will be set by EF Core.
    }
#pragma warning restore CS8618

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

        if (nutrition is not null && string.IsNullOrWhiteSpace(referenceUnit))
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

        if (nutrition is not null && string.IsNullOrWhiteSpace(referenceUnit))
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

        if (nutrition is not null && string.IsNullOrWhiteSpace(referenceUnit))
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

        if (nutrition is not null && string.IsNullOrWhiteSpace(referenceUnit))
        {
            throw new ArgumentException("Reference unit is required.", nameof(referenceUnit));
        }

        Name = name.Trim();
        ReferenceUnit = referenceUnit.Trim();
        Nutrition = nutrition;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }
}
