namespace LifeOS.Application.Stores;

/// <summary>
/// Read model returned by the API for a grocery store, mirroring the frontend's <c>GroceryStore</c> shape.
/// </summary>
public sealed record StoreDto(
    Guid Id,
    string Name,
    string Address,
    bool IsOrganic,
    bool IsLocal,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
