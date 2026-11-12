using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LifeOS.Api.Authentication;

/// <summary>
/// Authentication of the MCP endpoint (ADR 0011): requests are authenticated like the API, but
/// a missing or invalid token gets the challenge MCP clients expect, a 401 whose
/// WWW-Authenticate header points at the protected resource metadata (RFC 9728), so Claude
/// can find the authorization server and start sign-in.
/// </summary>
public static class AssistantAuthentication
{
    public const string Scheme = "LifeOS.Assistant";
    public const string ChallengeScheme = "LifeOS.AssistantChallenge";
    public const string Policy = "Assistant";
    public const string MetadataPath = "/.well-known/oauth-protected-resource";

    public static AuthenticationBuilder AddAssistantSchemes(this AuthenticationBuilder builder, string authenticateScheme) => builder
        .AddPolicyScheme(Scheme, null, policy =>
        {
            policy.ForwardAuthenticate = authenticateScheme;
            policy.ForwardChallenge = ChallengeScheme;
            policy.ForwardForbid = authenticateScheme;
        })
        .AddScheme<AuthenticationSchemeOptions, ChallengeHandler>(ChallengeScheme, _ => { });

    /// <summary>The protected resource metadata, at the root and path-suffixed well-known URLs.</summary>
    public static void MapProtectedResourceMetadata(this WebApplication app)
    {
        IResult Metadata(HttpContext context, IOptions<AuthOptions> options)
        {
            var resource = options.Value.McpResource;
            return Results.Json(new Dictionary<string, object>
            {
                ["resource"] = resource.AbsoluteUri,
                ["authorization_servers"] = new[] { IssuerOf(context, options.Value) },
                ["scopes_supported"] = new[] { OAuthClients.ApiScope },
                ["bearer_methods_supported"] = new[] { "header" },
                ["resource_name"] = "LifeOS",
            });
        }
        app.MapGet(MetadataPath, Metadata).AllowAnonymous().ExcludeFromDescription();
        app.MapGet(MetadataPath + "/mcp", Metadata).AllowAnonymous().ExcludeFromDescription();
    }

    /// <summary>The issuer exactly as OpenIddict publishes it (configured, else the request base URL).</summary>
    public static string IssuerOf(HttpContext context, AuthOptions options) =>
        !string.IsNullOrWhiteSpace(options.Issuer)
            ? new Uri(options.Issuer.TrimEnd('/') + "/").AbsoluteUri
            : $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}/";

    private sealed class ChallengeHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IOptions<AuthOptions> authOptions)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var metadata = IssuerOf(Context, authOptions.Value) + MetadataPath.TrimStart('/') + "/mcp";
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.Headers.WWWAuthenticate =
                $"Bearer error=\"invalid_token\", resource_metadata=\"{metadata}\", scope=\"{OAuthClients.ApiScope}\"";
            return Task.CompletedTask;
        }
    }
}
