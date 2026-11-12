using LifeOS.Domain.Common;

namespace LifeOS.Domain.Food.Products;

/// <summary>
/// A price paid for one package of a product in a store on a date, typed by hand. It freezes the
/// package contents of that moment, so editing the product never rewrites past prices.
/// </summary>
public sealed class ProductPrice : Entity
{
    public Guid ProductId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid StoreId { get; private set; }
    public decimal Price { get; private set; }
    public DateOnly ObservedOn { get; private set; }
    public decimal PackageQuantity { get; private set; }
    public string PackageUnit { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Package contents in comparison units (kg, L or pieces).</summary>
    public BaseQuantity Basis => Measures.ToBase(PackageQuantity, PackageUnit)
        ?? throw new InvalidOperationException($"Unité de contenance inconnue : {PackageUnit}.");

    /// <summary>Price per kilogram, litre or piece.</summary>
    public decimal UnitPrice => Price / Basis.Amount;

    private ProductPrice() { }

    internal static ProductPrice Create(Product product, Guid storeId, decimal price, DateOnly observedOn, DateTimeOffset now)
    {
        if (storeId == Guid.Empty) throw new ArgumentException("Un prix est relevé dans un magasin.");
        if (price <= 0 || price >= 100000 || decimal.Round(price, 2) != price)
            throw new ArgumentException("Prix positif requis, en euros et centimes, inférieur à 100 000 €.");
        // One day of tolerance covers time zones ahead of UTC.
        if (observedOn > DateOnly.FromDateTime(now.UtcDateTime).AddDays(1))
            throw new ArgumentException("La date du relevé ne peut pas être dans le futur.");
        return new ProductPrice
        {
            Id = Guid.NewGuid(), ProductId = product.Id, HouseholdId = product.HouseholdId, StoreId = storeId,
            Price = price, ObservedOn = observedOn, PackageQuantity = product.PackageQuantity,
            PackageUnit = product.PackageUnit, CreatedAt = now,
        };
    }
}
