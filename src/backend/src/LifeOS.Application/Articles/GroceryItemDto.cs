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
/// Legacy purchase projection of the canonical product. New clients use FoodItemDto.
/// </summary>
public sealed record GroceryItemDto(
    Guid Id,
    string Name,
    string Description,
    string Unit,
    IReadOnlyList<GroceryPriceEntryDto> PriceHistory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
