using LifeOS.Domain.Common;

namespace LifeOS.Domain.Food.Products;

/// <summary>
/// A commercial reference of one food (ADR 0010). A product only exists complete: its food,
/// name and package contents are mandatory, so every price comes down to a price per kilogram,
/// litre or piece.
/// </summary>
public sealed class Product : Entity
{
    public const int MaxNameLength = 200;

    public Guid HouseholdId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Brand { get; private set; }
    public string? Barcode { get; private set; }
    public decimal PackageQuantity { get; private set; }
    public string PackageUnit { get; private set; } = "";
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<ProductPrice> Prices { get; private set; } = [];

    private Product() { }

    public static Product Create(Guid householdId, Guid foodItemId, string name, decimal packageQuantity,
        string packageUnit, string? brand = null, string? barcode = null, DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer requis.");
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var product = new Product { Id = Guid.NewGuid(), HouseholdId = householdId, CreatedAt = timestamp };
        product.Update(foodItemId, name, packageQuantity, packageUnit, brand, barcode, timestamp);
        return product;
    }

    /// <summary>Prices already recorded keep the package contents they were observed with.</summary>
    public void Update(Guid foodItemId, string name, decimal packageQuantity, string packageUnit,
        string? brand = null, string? barcode = null, DateTimeOffset? now = null)
    {
        if (foodItemId == Guid.Empty) throw new ArgumentException("Un produit appartient à un aliment.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Nom de produit requis ({MaxNameLength} caractères maximum).");
        if (!Measures.PackageUnits.Contains(packageUnit))
            throw new ArgumentException("Unité de contenance : g, kg, ml, L ou pièce.");
        Measures.RequirePositive(packageQuantity, "Contenance positive requise (6 décimales maximum).");
        if (brand?.Trim().Length > 100 || barcode?.Trim().Length > 50)
            throw new ArgumentException("Marque (100 caractères) ou code-barres (50 caractères) trop long.");
        FoodItemId = foodItemId;
        Name = name.Trim();
        PackageQuantity = packageQuantity;
        PackageUnit = packageUnit;
        Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public ProductPrice RecordPrice(Guid storeId, decimal price, DateOnly observedOn, DateTimeOffset? now = null)
    {
        if (IsArchived) throw new ArgumentException("Produit archivé : relevé impossible.");
        var entry = ProductPrice.Create(this, storeId, price, observedOn, now ?? DateTimeOffset.UtcNow);
        Prices.Add(entry);
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        return entry;
    }

    /// <summary>Returns false when the price is not part of this product.</summary>
    public bool RemovePrice(Guid priceId)
    {
        var price = Prices.SingleOrDefault(p => p.Id == priceId);
        if (price is null) return false;
        Prices.Remove(price);
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>The most recent price of each store.</summary>
    public IReadOnlyList<ProductPrice> LatestPricePerStore() => Prices
        .GroupBy(p => p.StoreId)
        .Select(g => g.OrderByDescending(p => p.ObservedOn).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id).First())
        .ToList();

    public void Archive() { IsArchived = true; UpdatedAt = DateTimeOffset.UtcNow; }
}
