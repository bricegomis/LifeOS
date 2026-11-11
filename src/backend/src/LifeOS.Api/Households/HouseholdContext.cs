using LifeOS.Api.Authentication;
using LifeOS.Application.Households;

namespace LifeOS.Api.Households;

/// <summary>
/// The caller's identity and household, resolved once per request. Declare it as an endpoint
/// parameter instead of resolving the household by hand: every household-scoped query and
/// command must filter on <see cref="HouseholdId"/>.
/// </summary>
public sealed record HouseholdContext(Guid UserId, Guid HouseholdId)
{
    /// <summary>Minimal API binding hook; the household is provisioned on first sight.</summary>
    public static async ValueTask<HouseholdContext?> BindAsync(HttpContext context)
    {
        if (!context.User.TryGetUserId(out var userId))
            throw new UnauthorizedAccessException("Identifiant utilisateur absent du jeton.");
        var resolve = context.RequestServices.GetRequiredService<ResolveHouseholdForUserQuery>();
        return new HouseholdContext(userId, await resolve.ExecuteAsync(userId, context.RequestAborted));
    }
}
