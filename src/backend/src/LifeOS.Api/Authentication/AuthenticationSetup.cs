using System.Security.Claims;
using System.Security.Cryptography;
using LifeOS.Application.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation.AspNetCore;

namespace LifeOS.Api.Authentication;

/// <summary>
/// LifeOS is its own OAuth 2.1 server (ADR 0011). A short-lived cookie remembers the Google
/// sign-in while an authorization request is being answered; API calls carry an access token
/// issued and validated by OpenIddict.
/// </summary>
public static class AuthenticationSetup
{
    public const string SessionScheme = "LifeOS.Session";

    /// <summary>Query flag added to the authorization request when Google sign-in was refused or cancelled.</summary>
    public const string DeniedFlag = "lifeos_denied";

    public static void AddLifeOSAuthentication(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var environment = builder.Environment;
        var section = builder.Configuration.GetSection(AuthOptions.SectionName);
        services.Configure<AuthOptions>(section);
        var options = section.Get<AuthOptions>() ?? new AuthOptions();
        var isTesting = environment.IsEnvironment("Testing");
        var useEphemeralKeys = environment.IsDevelopment() || isTesting;

        services.AddSingleton(sp => new AccountAccessPolicy(sp.GetRequiredService<IOptions<AuthOptions>>().Value.AllowedEmails));

        // Behind the Coolify proxy: keep the public scheme and host for issuer and Google redirect URIs.
        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
        });

        services.AddOpenIddict()
            .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<LifeOSDbContext>().ReplaceDefaultEntities<Guid>())
            .AddServer(server =>
            {
                server.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetRevocationEndpointUris("connect/revoke");
                server.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().RequireProofKeyForCodeExchange();
                // PKCE S256 only (OAuth 2.1, ADR 0011): never the "plain" method.
                server.Configure(settings => settings.CodeChallengeMethods.Remove(
                    OpenIddict.Abstractions.OpenIddictConstants.CodeChallengeMethods.Plain));
                server.RegisterScopes(
                    OpenIddict.Abstractions.OpenIddictConstants.Scopes.OpenId,
                    OpenIddict.Abstractions.OpenIddictConstants.Scopes.Email,
                    OpenIddict.Abstractions.OpenIddictConstants.Scopes.Profile,
                    OpenIddict.Abstractions.OpenIddictConstants.Scopes.OfflineAccess,
                    OAuthClients.ApiScope);
                server.SetAccessTokenLifetime(TimeSpan.FromHours(1));
                // Refresh tokens rotate at each use, so this is an inactivity limit.
                server.SetRefreshTokenLifetime(TimeSpan.FromDays(60));

                if (!string.IsNullOrWhiteSpace(options.Issuer))
                    server.SetIssuer(new Uri(options.Issuer));

                if (useEphemeralKeys && (string.IsNullOrWhiteSpace(options.SigningKey) || string.IsNullOrWhiteSpace(options.EncryptionKey)))
                {
                    server.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
                }
                else
                {
                    server.AddSigningKey(ReadSigningKey(options.SigningKey));
                    server.AddEncryptionKey(ReadEncryptionKey(options.EncryptionKey));
                }

                var aspNetCore = server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough();
                if (useEphemeralKeys)
                    aspNetCore.DisableTransportSecurityRequirement();
            })
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.UseAspNetCore();
            });

        if (isTesting)
        {
            // Integration tests authenticate via TestAuthHandler instead of real tokens, so
            // several simulated users and households can be exercised deterministically.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
                .AddCookie(SessionScheme);
        }
        else
        {
            var authentication = services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .AddCookie(SessionScheme, cookie =>
                {
                    cookie.Cookie.Name = "lifeos.session";
                    cookie.Cookie.SameSite = SameSiteMode.Lax;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    // Only needed while an authorization request is answered and to skip
                    // Google on the next ones; tokens carry the long-lived session.
                    cookie.ExpireTimeSpan = TimeSpan.FromDays(1);
                    cookie.SlidingExpiration = false;
                });

            if (!string.IsNullOrWhiteSpace(options.Google.ClientId))
            {
                authentication.AddGoogle(GoogleDefaults.AuthenticationScheme, google =>
                {
                    google.SignInScheme = SessionScheme;
                    google.ClientId = options.Google.ClientId;
                    google.ClientSecret = options.Google.ClientSecret ?? string.Empty;
                    google.ClaimActions.MapJsonKey("email_verified", "email_verified");
                    google.Events.OnTicketReceived = OnGoogleTicketReceivedAsync;
                    google.Events.OnRemoteFailure = context =>
                    {
                        context.Response.Redirect(WithDeniedFlag(context.Properties?.RedirectUri));
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                });
            }
            else if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Auth:Google:ClientId and Auth:Google:ClientSecret are required.");
            }
        }

        services.AddAuthorization();
    }

    /// <summary>
    /// Replaces the Google identity with the LifeOS account; refuses unlisted or unverified
    /// addresses by sending the user back to the authorization request with the denied flag.
    /// </summary>
    private static async Task OnGoogleTicketReceivedAsync(TicketReceivedContext context)
    {
        var google = context.Principal!;
        var identity = new GoogleIdentity(
            google.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            google.FindFirstValue(ClaimTypes.Email),
            string.Equals(google.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            google.FindFirstValue(ClaimTypes.Name));

        var signIn = context.HttpContext.RequestServices.GetRequiredService<SignInWithGoogle>();
        var userId = await signIn.ExecuteAsync(identity, context.HttpContext.RequestAborted);
        if (userId is null)
        {
            context.Response.Redirect(WithDeniedFlag(context.ReturnUri));
            context.HandleResponse();
            return;
        }

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], SessionScheme));
    }

    private static string WithDeniedFlag(string? returnUri)
    {
        // Return URIs are the local authorization request set by the challenge; anything else goes home.
        if (string.IsNullOrEmpty(returnUri) || !returnUri.StartsWith("/connect/authorize", StringComparison.Ordinal))
            return "/";
        return returnUri + (returnUri.Contains('?') ? "&" : "?") + DeniedFlag + "=1";
    }

    public static void UseLifeOSForwardedHeaders(this WebApplication app) => app.UseForwardedHeaders();

    private static SecurityKey ReadSigningKey(string? pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException("Auth:SigningKey (RSA private key, PEM) is required.");
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem.Replace("\\n", "\n"));
        return new RsaSecurityKey(rsa);
    }

    private static SecurityKey ReadEncryptionKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException("Auth:EncryptionKey (32 bytes, base64) is required.");
        var bytes = Convert.FromBase64String(base64.Trim());
        if (bytes.Length != 32)
            throw new InvalidOperationException("Auth:EncryptionKey must decode to 32 bytes.");
        return new SymmetricSecurityKey(bytes);
    }
}
