using LifeOS.Domain.Common;

namespace LifeOS.Domain.Stock;

/// <summary>
/// Represents a stock item (quantité disponible d'un article au foyer).
/// Aggregate root of the Stock bounded context.
/// </summary>
public sealed class StockItem : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid GroceryItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private StockItem(
        Guid id,
        Guid householdId,
        Guid groceryItemId,
        decimal quantity,
        string unit,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        GroceryItemId = groceryItemId;
        Quantity = quantity;
        Unit = unit;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static StockItem Create(
        Guid householdId,
        Guid groceryItemId,
        decimal quantity,
        string unit,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A stock item must belong to a household.", nameof(householdId));
        }

        if (groceryItemId == Guid.Empty)
        {
            throw new ArgumentException("A stock item must reference a grocery item.", nameof(groceryItemId));
        }

        if (quantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(quantity));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Stock unit is required.", nameof(unit));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new StockItem(
            Guid.NewGuid(),
            householdId,
            groceryItemId,
            quantity,
            unit.Trim(),
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Rehydrates a <see cref="StockItem"/> from persisted state.
    /// </summary>
    public static StockItem Rehydrate(
        Guid id,
        Guid householdId,
        Guid groceryItemId,
        decimal quantity,
        string unit,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new StockItem(
            id,
            householdId,
            groceryItemId,
            quantity,
            unit,
            createdAt,
            updatedAt);
    }

    public void UpdateQuantity(decimal quantity, DateTimeOffset? now = null)
    {
        if (quantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(quantity));
        }

        Quantity = quantity;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void UpdateUnit(string unit, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Stock unit is required.", nameof(unit));
        }

        Unit = unit.Trim();
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }
}
