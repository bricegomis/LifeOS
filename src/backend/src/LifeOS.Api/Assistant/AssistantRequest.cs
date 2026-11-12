using LifeOS.Api.Households;

namespace LifeOS.Api.Assistant;

/// <summary>
/// The caller of an MCP tool: the signed-in account and its household, resolved exactly as for
/// an endpoint (<see cref="HouseholdContext"/>). Every tool filters on its household.
/// </summary>
public static class AssistantRequest
{
    public static async Task<HouseholdContext> HouseholdAsync(IHttpContextAccessor accessor) =>
        await HouseholdContext.BindAsync(accessor.HttpContext
            ?? throw new InvalidOperationException("MCP tools run inside an HTTP request."))
        ?? throw new UnauthorizedAccessException("Foyer introuvable.");
}
