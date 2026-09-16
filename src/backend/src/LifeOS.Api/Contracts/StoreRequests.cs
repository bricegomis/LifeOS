namespace LifeOS.Api.Contracts;

/// <summary>
/// Request body for creating or updating a grocery store.
/// </summary>
public sealed record StoreRequest(string Name, string Address, bool IsOrganic, bool IsLocal);
