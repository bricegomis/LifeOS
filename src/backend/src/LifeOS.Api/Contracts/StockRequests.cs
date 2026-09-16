namespace LifeOS.Api.Contracts;

public sealed record CreateStockItemRequest(
    Guid GroceryItemId,
    decimal Quantity,
    string Unit);

public sealed record UpdateStockItemRequest(
    decimal Quantity,
    string? Unit);
