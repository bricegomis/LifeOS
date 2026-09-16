using System.ComponentModel.DataAnnotations;

namespace LifeOS.Api.Contracts;

public sealed record CreateStockItemRequest(
    Guid GroceryItemId,
    [property: Range(0, double.MaxValue)] decimal Quantity,
    [property: Required, StringLength(20)] string Unit);

public sealed record UpdateStockItemRequest(
    [property: Range(0, double.MaxValue)] decimal Quantity,
    [property: StringLength(20, MinimumLength = 1)] string? Unit);
