using LifeOS.Domain.Common;

namespace LifeOS.Domain.Stock;

/// <summary>
/// Represents a shopping list item (article à acheter pour une semaine).
/// Aggregate root of the Shopping List bounded context.
/// </summary>
public sealed class ShoppingListItem : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid? WeekId { get; private set; }
    public Guid GroceryItemId { get; private set; }
    public decimal QuantityNeeded { get; private set; }
    public decimal QuantityFromStock { get; private set; }
    public bool Checked { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ShoppingListItem(
        Guid id,
        Guid householdId,
        Guid? weekId,
        Guid groceryItemId,
        decimal quantityNeeded,
        decimal quantityFromStock,
        bool @checked,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        WeekId = weekId;
        GroceryItemId = groceryItemId;
        QuantityNeeded = quantityNeeded;
        QuantityFromStock = quantityFromStock;
        Checked = @checked;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static ShoppingListItem Create(
        Guid householdId,
        Guid? weekId,
        Guid groceryItemId,
        decimal quantityNeeded,
        decimal quantityFromStock,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A shopping list item must belong to a household.", nameof(householdId));
        }

        if (groceryItemId == Guid.Empty)
        {
            throw new ArgumentException("A shopping list item must reference a grocery item.", nameof(groceryItemId));
        }

        if (quantityNeeded < 0)
        {
            throw new ArgumentException("Quantity needed cannot be negative.", nameof(quantityNeeded));
        }

        if (quantityFromStock < 0)
        {
            throw new ArgumentException("Quantity from stock cannot be negative.", nameof(quantityFromStock));
        }

        if (quantityFromStock > quantityNeeded)
        {
            throw new ArgumentException("Quantity from stock cannot exceed quantity needed.", nameof(quantityFromStock));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new ShoppingListItem(
            Guid.NewGuid(),
            householdId,
            weekId,
            groceryItemId,
            quantityNeeded,
            quantityFromStock,
            false,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Rehydrates a <see cref="ShoppingListItem"/> from persisted state.
    /// </summary>
    public static ShoppingListItem Rehydrate(
        Guid id,
        Guid householdId,
        Guid? weekId,
        Guid groceryItemId,
        decimal quantityNeeded,
        decimal quantityFromStock,
        bool @checked,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new ShoppingListItem(
            id,
            householdId,
            weekId,
            groceryItemId,
            quantityNeeded,
            quantityFromStock,
            @checked,
            createdAt,
            updatedAt);
    }

    public void UpdateChecked(bool @checked, DateTimeOffset? now = null)
    {
        Checked = @checked;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public decimal GetQuantityToBuy() => QuantityNeeded - QuantityFromStock;
}
