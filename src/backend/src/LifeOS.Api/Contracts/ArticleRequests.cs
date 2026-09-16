using System.ComponentModel.DataAnnotations;

namespace LifeOS.Api.Contracts;

/// <summary>
/// Request body for creating or updating a grocery article.
/// </summary>
public sealed record ArticleRequest(
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(1000)] string Description,
    [property: Required, StringLength(20)] string Unit);

/// <summary>
/// Request body for recording a new observed price for a grocery article.
/// </summary>
public sealed record PriceEntryRequest(
    Guid StoreId,
    [property: Range(0, double.MaxValue)] decimal Price,
    DateTimeOffset ObservedAt);
