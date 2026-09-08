using LifeOS.Domain.Common;

namespace LifeOS.Domain.Stores;

/// <summary>
/// A grocery store where a user buys food, mirroring the frontend's <c>GroceryStore</c> model.
/// Aggregate root of the Stores bounded context.
/// </summary>
public sealed class Store : Entity
{
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; }
    public string Address { get; private set; }
    public bool IsOrganic { get; private set; }
    public bool IsLocal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Store(
        Guid id,
        Guid ownerId,
        string name,
        string address,
        bool isOrganic,
        bool isLocal,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        Address = address;
        IsOrganic = isOrganic;
        IsLocal = isLocal;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Store Create(
        Guid ownerId,
        string name,
        string address,
        bool isOrganic,
        bool isLocal,
        DateTimeOffset? now = null)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("A store must belong to an owner.", nameof(ownerId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Store name is required.", nameof(name));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new Store(
            Guid.NewGuid(),
            ownerId,
            name.Trim(),
            address.Trim(),
            isOrganic,
            isLocal,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Rehydrates a <see cref="Store"/> from persisted state.
    /// </summary>
    public static Store Rehydrate(
        Guid id,
        Guid ownerId,
        string name,
        string address,
        bool isOrganic,
        bool isLocal,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Store(id, ownerId, name, address, isOrganic, isLocal, createdAt, updatedAt);
    }
}
