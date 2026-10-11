using LifeOS.Domain.Articles;

namespace LifeOS.Domain.Products;

public sealed record ComparablePrice(Guid ProductId, string ProductName, Guid StoreId, decimal Price,
    string Unit, DateTimeOffset ObservedAt, string Provenance, Guid? ReceiptLineId);

public static class ComparablePrices
{
    public static IReadOnlyList<ComparablePrice> Minimums(IEnumerable<Product> products) =>
        products.Where(p => !p.IsArchived).SelectMany(p => p.PriceHistory
            .GroupBy(e => e.StoreId)
            .Select(g => g.OrderByDescending(e => e.ObservedAt).ThenByDescending(e => e.CreatedAt).ThenBy(e => e.Id).First())
            .Select(e => Normalize(p, e)))
        .Where(p => p is not null).Select(p => p!)
        .GroupBy(p => p.Unit).Select(g => g.OrderBy(p => p.Price).ThenByDescending(p => p.ObservedAt).First()).ToList();

    private static ComparablePrice? Normalize(Product p, GroceryPriceEntry e)
    {
        var unit = e.Unit;
        decimal price = e.Price;
        if (unit == "g") { price *= 1000; unit = "kg"; }
        else if (unit == "ml") { price *= 1000; unit = "L"; }
        else if (unit is not ("kg" or "L"))
        {
            if (e.PackageConfirmed && e.PackageQuantity is > 0 && e.PackageUnit is "g" or "kg" or "ml" or "L")
            {
                var factor = e.PackageUnit is "g" or "ml" ? 1000m : 1m;
                price = price * factor / e.PackageQuantity.Value;
                unit = e.PackageUnit is "g" or "kg" ? "kg" : "L";
            }
            else if (unit == "unit" && e.PiecesComparable) unit = "piece";
            else return null;
        }
        return new(p.Id, p.Name, e.StoreId, price, unit, e.ObservedAt, e.Provenance, e.ReceiptLineId);
    }
}
