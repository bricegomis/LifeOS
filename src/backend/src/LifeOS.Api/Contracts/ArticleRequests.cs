namespace LifeOS.Api.Contracts;

/// <summary>
/// Request body for creating or updating a grocery article.
/// </summary>
public sealed record ArticleRequest(string Name, string Description, string Unit);

/// <summary>
/// Request body for recording a new observed price for a grocery article.
/// </summary>
public sealed record PriceEntryRequest(Guid StoreId, decimal Price, DateTimeOffset ObservedAt);
