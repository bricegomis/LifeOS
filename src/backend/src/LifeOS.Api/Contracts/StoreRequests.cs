using System.ComponentModel.DataAnnotations;

namespace LifeOS.Api.Contracts;

/// <summary>
/// Request body for creating or updating a grocery store.
/// </summary>
public sealed record StoreRequest(
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(400)] string Address,
    bool IsOrganic,
    bool IsLocal);
