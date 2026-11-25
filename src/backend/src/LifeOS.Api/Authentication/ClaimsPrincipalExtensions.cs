using System.Security.Claims;

namespace LifeOS.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the LifeOS account id (the "sub" claim of a token issued by the API) from the current principal.
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId);
    }
}
