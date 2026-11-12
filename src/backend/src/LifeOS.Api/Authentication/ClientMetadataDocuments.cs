using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LifeOS.Api.Authentication;

/// <summary>A client ID metadata document (draft-ietf-oauth-client-id-metadata-document), once validated.</summary>
public sealed record ClientMetadata(Uri ClientId, string? ClientName, IReadOnlyList<Uri> RedirectUris);

/// <summary>
/// Claude identifies itself with an HTTPS client_id that points to its metadata document
/// (CIMD). LifeOS accepts it only from trusted hosts, reads and checks the document when an
/// authorization request arrives, and registers the client in OpenIddict just in time, so the
/// regular OpenIddict validation then applies (ADR 0011). No dynamic registration endpoint.
/// </summary>
public static class ClientMetadataDocuments
{
    public const string HttpClientName = "client-metadata";
    private const int MaxDocumentBytes = 16 * 1024;

    /// <summary>Is <paramref name="clientId"/> an HTTPS URL on a trusted host (no user info, no fragment)?</summary>
    public static bool IsTrustedClientId(string? clientId, IEnumerable<string> trustedHosts, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(clientId, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(parsed.UserInfo) || !string.IsNullOrEmpty(parsed.Fragment)
            || parsed.AbsolutePath == "/") return false;
        if (!trustedHosts.Any(host => string.Equals(host, parsed.Host, StringComparison.OrdinalIgnoreCase))) return false;
        uri = parsed;
        return true;
    }

    /// <summary>Parses and checks a metadata document: its client_id must be the URL it was read from.</summary>
    public static ClientMetadata? Parse(Uri clientId, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("client_id", out var id) || id.GetString() != clientId.AbsoluteUri) return null;
            if (root.TryGetProperty("token_endpoint_auth_method", out var method) && method.GetString() is { } m && m != "none") return null;
            if (!root.TryGetProperty("redirect_uris", out var redirects) || redirects.ValueKind != JsonValueKind.Array) return null;

            var uris = new List<Uri>();
            foreach (var item in redirects.EnumerateArray())
            {
                if (!Uri.TryCreate(item.GetString(), UriKind.Absolute, out var redirect)) return null;
                // Only HTTPS, or HTTP to this computer (native apps such as Claude Code).
                if (redirect.Scheme != Uri.UriSchemeHttps && !IsLoopback(redirect)) return null;
                uris.Add(redirect);
            }
            if (uris.Count == 0) return null;

            var name = root.TryGetProperty("client_name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            return new ClientMetadata(clientId, name, uris);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsLoopback(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp && (uri.Host is "localhost" or "127.0.0.1" or "[::1]" || uri.IsLoopback);

    /// <summary>
    /// Exact match, except that a loopback redirect matches whatever its port (RFC 8252 § 7.3;
    /// Claude Code binds an ephemeral port and declares localhost and 127.0.0.1).
    /// </summary>
    public static bool AllowsRedirect(ClientMetadata metadata, Uri requested) =>
        metadata.RedirectUris.Any(declared =>
            declared == requested
            || (IsLoopback(declared) && IsLoopback(requested)
                && string.Equals(declared.Host, requested.Host, StringComparison.OrdinalIgnoreCase)
                && declared.AbsolutePath == requested.AbsolutePath
                && string.IsNullOrEmpty(requested.Query)));

    /// <summary>
    /// Registers the client the authorization request names, when it is a trusted metadata URL.
    /// Runs before OpenIddict validates the request; on any failure it does nothing and
    /// OpenIddict refuses the unknown client.
    /// </summary>
    public static async Task RegisterFromAuthorizationRequestAsync(HttpContext context)
    {
        var request = context.Request;
        string? clientId, redirectUri;
        if (HttpMethods.IsPost(request.Method) && request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(context.RequestAborted);
            clientId = form[Parameters.ClientId];
            redirectUri = form[Parameters.RedirectUri];
        }
        else
        {
            clientId = request.Query[Parameters.ClientId];
            redirectUri = request.Query[Parameters.RedirectUri];
        }

        var options = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
        if (!IsTrustedClientId(clientId, options.TrustedClientMetadataHosts, out var clientUri)
            || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var requestedRedirect))
            return;

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ClientMetadataDocuments));
        var metadata = await FetchAsync(context, clientUri, logger);
        if (metadata is null) return;
        if (!AllowsRedirect(metadata, requestedRedirect))
        {
            logger.LogWarning("Redirect URI {RedirectUri} is not declared by {ClientId}.", requestedRedirect, clientUri);
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientUri.AbsoluteUri,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = string.IsNullOrWhiteSpace(metadata.ClientName) ? clientUri.Host : $"{metadata.ClientName.Trim()} ({clientUri.Host})",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + OAuthClients.ApiScope,
                Permissions.Prefixes.Resource + options.McpResource.AbsoluteUri,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        // Declared HTTPS redirects, plus the loopback one of this request: OpenIddict compares
        // redirect URIs exactly, and Claude Code's port changes at each session.
        foreach (var uri in metadata.RedirectUris.Where(uri => !IsLoopback(uri))) descriptor.RedirectUris.Add(uri);
        if (IsLoopback(requestedRedirect)) descriptor.RedirectUris.Add(requestedRedirect);

        var manager = context.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
        var existing = await manager.FindByClientIdAsync(descriptor.ClientId, context.RequestAborted);
        if (existing is null)
            await manager.CreateAsync(descriptor, context.RequestAborted);
        else
            await manager.UpdateAsync(existing, descriptor, context.RequestAborted);
    }

    private static async Task<ClientMetadata?> FetchAsync(HttpContext context, Uri clientUri, ILogger logger)
    {
        try
        {
            var http = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            using var response = await http.GetAsync(clientUri, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > MaxDocumentBytes)
            {
                logger.LogWarning("Client metadata document {ClientId} answered {Status}.", clientUri, (int)response.StatusCode);
                return null;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
            var buffer = new byte[MaxDocumentBytes + 1];
            var read = 0;
            int chunk;
            while (read < buffer.Length && (chunk = await stream.ReadAsync(buffer.AsMemory(read), context.RequestAborted)) > 0) read += chunk;
            if (read > MaxDocumentBytes) return null;

            var metadata = Parse(clientUri, System.Text.Encoding.UTF8.GetString(buffer, 0, read));
            if (metadata is null) logger.LogWarning("Client metadata document {ClientId} is invalid.", clientUri);
            return metadata;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Client metadata document {ClientId} could not be read.", clientUri);
            return null;
        }
    }
}
