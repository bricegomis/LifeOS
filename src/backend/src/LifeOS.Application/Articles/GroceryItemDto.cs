namespace LifeOS.Application.Articles;

/// <summary>
/// Read model returned by the API for a price observation, mirroring the frontend's
/// <c>GroceryPriceEntry</c> shape.
/// </summary>
public sealed record GroceryPriceEntryDto(
    Guid Id,
    Guid StoreId,
    decimal Price,
    DateTimeOffset ObservedAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Read model returned by the API for a grocery article, mirroring the frontend's
/// <c>GroceryItem</c> shape.
/// </summary>
public sealed record GroceryItemDto(
    Guid Id,
    string Name,
    string Description,
    string Unit,
    IReadOnlyList<GroceryPriceEntryDto> PriceHistory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
