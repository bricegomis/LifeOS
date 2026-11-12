using LifeOS.Domain.Food.Products;

namespace LifeOS.Application.Food.Products;

/// <summary>Data the price use cases need; implemented by Infrastructure on the Food tables.</summary>
public interface IProductPriceData
{
    /// <summary>The household's product with its prices, or null.</summary>
    Task<Product?> FindProductAsync(Guid householdId, Guid productId, CancellationToken ct);
    Task<bool> StoreBelongsToHouseholdAsync(Guid householdId, Guid storeId, CancellationToken ct);
    void AddPrice(ProductPrice price);
    void RemovePrice(ProductPrice price);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>
/// Records the price paid for one package of a product, typed by hand (ADR 0010). The store
/// must belong to the caller's household; the price freezes the product's package contents.
/// </summary>
public sealed class RecordProductPrice(IProductPriceData data, TimeProvider clock)
{
    /// <returns>The updated product, or null when the product is not in the household.</returns>
    public async Task<Product?> ExecuteAsync(Guid householdId, Guid productId, Guid storeId, decimal price,
        DateOnly observedOn, CancellationToken ct)
    {
        var product = await data.FindProductAsync(householdId, productId, ct);
        if (product is null) return null;
        if (!await data.StoreBelongsToHouseholdAsync(householdId, storeId, ct))
            throw new ArgumentException("Magasin indisponible dans votre foyer.");
        data.AddPrice(product.RecordPrice(storeId, price, observedOn, clock.GetUtcNow()));
        await data.SaveAsync(ct);
        return product;
    }
}

/// <summary>Deletes a mistyped price.</summary>
public sealed class RemoveProductPrice(IProductPriceData data)
{
    /// <returns>The updated product, or null when the product or the price is not found.</returns>
    public async Task<Product?> ExecuteAsync(Guid householdId, Guid productId, Guid priceId, CancellationToken ct)
    {
        var product = await data.FindProductAsync(householdId, productId, ct);
        var price = product?.Prices.SingleOrDefault(p => p.Id == priceId);
        if (product is null || price is null) return null;
        product.RemovePrice(priceId);
        data.RemovePrice(price);
        await data.SaveAsync(ct);
        return product;
    }
}
