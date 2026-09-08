namespace LifeOS.Domain.Articles;

/// <summary>
/// A price observed for a <see cref="GroceryItem"/> at a given store and date, mirroring the
/// frontend's <c>GroceryPriceEntry</c> model.
/// </summary>
public sealed class GroceryPriceEntry
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public decimal Price { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private GroceryPriceEntry(Guid id, Guid storeId, decimal price, DateTimeOffset observedAt, DateTimeOffset createdAt)
    {
        Id = id;
        StoreId = storeId;
        Price = price;
        ObservedAt = observedAt;
        CreatedAt = createdAt;
    }

    public static GroceryPriceEntry Create(Guid storeId, decimal price, DateTimeOffset observedAt, DateTimeOffset? now = null)
    {
        if (storeId == Guid.Empty)
        {
            throw new ArgumentException("A price entry must reference a store.", nameof(storeId));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        return new GroceryPriceEntry(Guid.NewGuid(), storeId, price, observedAt, now ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Rehydrates a <see cref="GroceryPriceEntry"/> from persisted state.
    /// </summary>
    public static GroceryPriceEntry Rehydrate(Guid id, Guid storeId, decimal price, DateTimeOffset observedAt, DateTimeOffset createdAt)
    {
        return new GroceryPriceEntry(id, storeId, price, observedAt, createdAt);
    }
}
