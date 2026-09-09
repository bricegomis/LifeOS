using LifeOS.Domain.Common;

namespace LifeOS.Domain.Articles;

/// <summary>
/// A grocery article tracked across stores, mirroring the frontend's <c>GroceryItem</c> model.
/// Aggregate root of the Articles bounded context; owns its <see cref="GroceryPriceEntry"/> price history.
/// </summary>
public sealed class GroceryItem : Entity
{
    private readonly List<GroceryPriceEntry> _priceHistory = [];

    public Guid OwnerId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public GroceryItemUnit Unit { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<GroceryPriceEntry> PriceHistory => _priceHistory;

    private GroceryItem(
        Guid id,
        Guid ownerId,
        string name,
        string description,
        GroceryItemUnit unit,
        IEnumerable<GroceryPriceEntry> priceHistory,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        Description = description;
        Unit = unit;
        _priceHistory.AddRange(priceHistory);
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static GroceryItem Create(
        Guid ownerId,
        string name,
        string description,
        GroceryItemUnit unit,
        DateTimeOffset? now = null)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("An article must belong to an owner.", nameof(ownerId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Article name is required.", nameof(name));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new GroceryItem(
            Guid.NewGuid(),
            ownerId,
            name.Trim(),
            description.Trim(),
            unit,
            [],
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Rehydrates a <see cref="GroceryItem"/> from persisted state.
    /// </summary>
    public static GroceryItem Rehydrate(
        Guid id,
        Guid ownerId,
        string name,
        string description,
        GroceryItemUnit unit,
        IEnumerable<GroceryPriceEntry> priceHistory,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new GroceryItem(id, ownerId, name, description, unit, priceHistory, createdAt, updatedAt);
    }

    public void UpdateDetails(string name, string description, GroceryItemUnit unit, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Article name is required.", nameof(name));
        }

        Name = name.Trim();
        Description = description.Trim();
        Unit = unit;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public GroceryPriceEntry AddPriceEntry(Guid storeId, decimal price, DateTimeOffset observedAt, DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var entry = GroceryPriceEntry.Create(storeId, price, observedAt, timestamp);

        _priceHistory.Add(entry);
        UpdatedAt = timestamp;

        return entry;
    }

    public bool RemovePriceEntry(Guid priceEntryId, DateTimeOffset? now = null)
    {
        var removed = _priceHistory.RemoveAll(entry => entry.Id == priceEntryId) > 0;

        if (removed)
        {
            UpdatedAt = now ?? DateTimeOffset.UtcNow;
        }

        return removed;
    }
}
