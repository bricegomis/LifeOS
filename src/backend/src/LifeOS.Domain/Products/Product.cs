using LifeOS.Domain.Common;
using LifeOS.Domain.Articles;

namespace LifeOS.Domain.Products;

public sealed class Product : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid? FoodItemId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string? Brand { get; private set; }
    public string? Barcode { get; private set; }
    public string PurchaseUnit { get; private set; } = "unit";
    public bool PurchaseUnitConfirmed { get; private set; }
    public decimal? PackageQuantity { get; private set; }
    public string? PackageUnit { get; private set; }
    public bool PackageConfirmed { get; private set; }
    public bool PiecesComparable { get; private set; }
    public bool IsArchived { get; private set; }
    public string? LegacyPurchaseName { get; private set; }
    public string? MigrationOrigin { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private readonly List<GroceryPriceEntry> _priceHistory = [];
    public IReadOnlyList<GroceryPriceEntry> PriceHistory => _priceHistory;

    private Product() { }

    public static Product Create(Guid householdId, Guid foodItemId, string name, string unit)
    {
        if (householdId == Guid.Empty || foodItemId == Guid.Empty) throw new ArgumentException("Foyer et aliment requis.");
        var product = new Product { Id = Guid.NewGuid(), HouseholdId = householdId, FoodItemId = foodItemId, CreatedAt = DateTimeOffset.UtcNow };
        product.Update(name, "", unit, null, null, null, null, false, false);
        return product;
    }

    public void LinkFood(Guid foodItemId)
    {
        if (foodItemId == Guid.Empty) throw new ArgumentException("Aliment requis.");
        if (FoodItemId is not null && FoodItemId != foodItemId) throw new ArgumentException("Ce produit appartient déjà à un aliment.");
        FoodItemId = foodItemId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(string name, string description, string unit, string? brand, string? barcode,
        decimal? packageQuantity, string? packageUnit, bool confirmed, bool piecesComparable)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 500) throw new ArgumentException("Nom de produit requis (500 caractères maximum).");
        if (description.Length > 1000 || brand?.Length > 200 || barcode?.Length > 50) throw new ArgumentException("Informations commerciales trop longues.");
        PurchaseUnits.Validate(unit);
        if (PurchaseUnitConfirmed && _priceHistory.Count > 0 && unit != PurchaseUnit) throw new ArgumentException("L'unité d'achat est figée après un relevé.");
        if (packageQuantity is not null && (packageQuantity <= 0 || packageQuantity > 1000000 || decimal.Round(packageQuantity.Value, 6) != packageQuantity)) throw new ArgumentException("Contenance invalide (6 décimales maximum).");
        if (packageUnit is not (null or "g" or "kg" or "ml" or "L")) throw new ArgumentException("Unité de contenance invalide.");
        if (confirmed && (packageQuantity is null || packageUnit is null)) throw new ArgumentException("Confirmez une contenance et son unité.");
        Name = name.Trim(); Description = description.Trim(); PurchaseUnit = unit;
        PurchaseUnitConfirmed = true;
        Brand = brand; Barcode = barcode; PackageQuantity = packageQuantity; PackageUnit = packageUnit;
        PackageConfirmed = confirmed; PiecesComparable = piecesComparable;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public GroceryPriceEntry ObservePrice(Guid storeId, decimal price, DateTimeOffset date, Guid? receiptLineId = null)
    {
        if (IsArchived) throw new ArgumentException("Produit archivé.");
        if (!PurchaseUnitConfirmed) throw new ArgumentException("Confirmez l'unité d'achat avant de relever un prix.");
        var entry = GroceryPriceEntry.Create(storeId, price, date);
        entry.SetBasis(PurchaseUnit, PackageQuantity, PackageUnit, PackageConfirmed, PiecesComparable, receiptLineId);
        _priceHistory.Add(entry);
        UpdatedAt = DateTimeOffset.UtcNow;
        return entry;
    }

    public void Archive() { IsArchived = true; UpdatedAt = DateTimeOffset.UtcNow; }
}

public static class PurchaseUnits
{
    public static void Validate(string unit)
    {
        if (unit is not ("kg" or "g" or "L" or "ml" or "unit" or "bag" or "box" or "pot" or "pack"))
            throw new ArgumentException("Unité d'achat inconnue.");
    }
}
