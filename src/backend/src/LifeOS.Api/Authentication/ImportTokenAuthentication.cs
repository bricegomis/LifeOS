using System.Security.Claims;
using System.Text.Encodings.Web;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LifeOS.Api.Authentication;

/// <summary>
/// Import services (garmin-sync, ADR 0019) authenticate with an import token in the
/// <see cref="HeaderName"/> header. The token acts for the account that created it, only on the
/// endpoints of the <see cref="Policy"/> policy; the app's other endpoints never accept it.
/// </summary>
public static class ImportTokenAuthentication
{
    public const string Scheme = "ImportToken";
    public const string Policy = "Import";
    public const string HeaderName = "X-LifeOS-Import-Token";
    public const string ProfileClaim = "lifeos_import_profile";

    public static void AddImportTokenAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ImportTokenHandler>(Scheme, _ => { });
        services.AddAuthorization(authorization => authorization.AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(ProfileClaim)));
    }

    /// <summary>The profile the token writes for.</summary>
    public static Guid ImportProfile(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ProfileClaim), out var id) ? id : throw new UnauthorizedAccessException("Jeton d'import absent.");
}

public sealed class ImportTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ImportTokenAuthentication.HeaderName, out var values) || string.IsNullOrWhiteSpace(values))
            return AuthenticateResult.NoResult();
        var secret = values.ToString().Trim();
        if (!secret.StartsWith(ImportToken.Prefix, StringComparison.Ordinal)) return AuthenticateResult.Fail("Jeton d'import invalide.");
        var db = Context.RequestServices.GetRequiredService<LifeOSDbContext>();
        var hash = ImportToken.HashOf(secret);
        var token = await db.ImportTokens.SingleOrDefaultAsync(t => t.Hash == hash && t.RevokedAt == null, Context.RequestAborted);
        if (token is null) return AuthenticateResult.Fail("Jeton d'import inconnu ou révoqué.");
        if (token.Touch(clock.GetUtcNow())) await db.SaveChangesAsync(Context.RequestAborted);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, token.UserId.ToString()), new Claim(ImportTokenAuthentication.ProfileClaim, token.MemberProfileId.ToString())],
            ImportTokenAuthentication.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ImportTokenAuthentication.Scheme));
    }
}
