using System.Security.Claims;

namespace LifeOS.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the authenticated Supabase user id from the "sub" claim of the current principal.
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId);
    }
}
