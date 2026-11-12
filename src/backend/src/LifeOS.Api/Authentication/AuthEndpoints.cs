using System.Collections.Immutable;
using System.Security.Claims;
using LifeOS.Application.Households;
using LifeOS.Domain.Households;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LifeOS.Api.Authentication;

/// <summary>
/// The OAuth endpoints whose answer depends on the user (ADR 0011). OpenIddict validates the
/// requests (client, redirect URI, PKCE) before they reach these handlers.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync).ExcludeFromDescription();
        app.MapPost("/connect/token", TokenAsync).ExcludeFromDescription();
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        GetActiveAccount getActiveAccount,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict authorization request is missing.");

        if (context.Request.Query.ContainsKey(AuthenticationSetup.DeniedFlag))
            return Forbid(Errors.AccessDenied, "Ce compte Google n’est pas autorisé à utiliser LifeOS, ou la connexion a été annulée.");

        var session = await context.AuthenticateAsync(AuthenticationSetup.SessionScheme);
        if (!session.Succeeded || request.HasPromptValue(PromptValues.Login) || !TryGetUserId(session.Principal, out var userId))
        {
            if (request.HasPromptValue(PromptValues.None))
                return Forbid(Errors.LoginRequired, "Connexion requise.");

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = AuthorizationRequestPath(context) },
                [GoogleDefaults.AuthenticationScheme]);
        }

        var account = await getActiveAccount.ExecuteAsync(userId, context.RequestAborted);
        if (account is null)
        {
            await context.SignOutAsync(AuthenticationSetup.SessionScheme);
            return Forbid(Errors.AccessDenied, "Ce compte n’est plus autorisé à utiliser LifeOS.");
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!, context.RequestAborted)
            ?? throw new InvalidOperationException("The client application cannot be found.");

        // Only the LifeOS app exists for now and it needs no consent; Claude clients and their
        // consent screen come with the MCP endpoint (roadmap step 1 bis, lot 2).
        if (await applications.GetConsentTypeAsync(application, context.RequestAborted) != ConsentTypes.Implicit)
            return Forbid(Errors.ConsentRequired, "Ce client n’est pas encore autorisé.");

        var applicationId = await applications.GetIdAsync(application, context.RequestAborted);
        var scopes = request.GetScopes();
        var principal = CreatePrincipal(account, scopes);

        // One permanent authorization per user and client: tokens hang off it, and revoking it
        // (settings, lot 2) revokes them all.
        var authorization = await FirstOrDefaultAsync(authorizations.FindAsync(
            account.Id.ToString(), applicationId!, Statuses.Valid, AuthorizationTypes.Permanent, scopes, context.RequestAborted))
            ?? await authorizations.CreateAsync(principal, account.Id.ToString(), applicationId!, AuthorizationTypes.Permanent, scopes, context.RequestAborted);
        principal.SetAuthorizationId(await authorizations.GetIdAsync(authorization, context.RequestAborted));

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(HttpContext context, GetActiveAccount getActiveAccount)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict token request is missing.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            return Forbid(Errors.UnsupportedGrantType, "Type de demande non pris en charge.");

        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!TryGetUserId(result.Principal, out var userId))
            return Forbid(Errors.InvalidGrant, "Jeton invalide.");

        // Re-checked at every refresh: an account removed from the allowed list loses access.
        var account = await getActiveAccount.ExecuteAsync(userId, context.RequestAborted);
        if (account is null)
            return Forbid(Errors.InvalidGrant, "Ce compte n’est plus autorisé à utiliser LifeOS.");

        var principal = CreatePrincipal(account, result.Principal!.GetScopes());
        principal.SetAuthorizationId(result.Principal!.GetAuthorizationId());
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static ClaimsPrincipal CreatePrincipal(UserAccount account, ImmutableArray<string> scopes)
    {
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, account.Id.ToString())
            .SetClaim(Claims.Email, account.Email)
            .SetClaim(Claims.Name, account.DisplayName ?? account.Email);
        identity.SetScopes(scopes);
        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Email when scopes.Contains(Scopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name when scopes.Contains(Scopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken],
        });
        return new ClaimsPrincipal(identity);
    }

    private static IResult Forbid(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    private static bool TryGetUserId(ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;
        return principal is not null && principal.TryGetUserId(out userId);
    }

    /// <summary>The current authorization request as a local URL (form posts become a query string).</summary>
    private static string AuthorizationRequestPath(HttpContext context)
    {
        var parameters = context.Request.HasFormContentType
            ? context.Request.Form.Where(pair => pair.Key != AuthenticationSetup.DeniedFlag).ToList()
            : context.Request.Query.Where(pair => pair.Key != AuthenticationSetup.DeniedFlag).ToList();
        return context.Request.PathBase + context.Request.Path + QueryString.Create(parameters);
    }

    private static async Task<object?> FirstOrDefaultAsync(IAsyncEnumerable<object> source)
    {
        await foreach (var item in source) return item;
        return null;
    }
}
