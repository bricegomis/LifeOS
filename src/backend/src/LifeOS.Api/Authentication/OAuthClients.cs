using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LifeOS.Api.Authentication;

/// <summary>
/// Clients are registered up front, never dynamically (ADR 0011). Called at startup after
/// migrations: creates or updates the LifeOS app client from configuration.
/// </summary>
public static class OAuthClients
{
    public const string ApiScope = "lifeos";

    public static async Task SynchronizeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var web = services.GetRequiredService<IOptions<AuthOptions>>().Value.WebClient;

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = AuthOptions.WebClientOptions.ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "LifeOS",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + ApiScope,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var uri in web.RedirectUris.Where(IsAbsolute)) descriptor.RedirectUris.Add(new Uri(uri));

        var existing = await manager.FindByClientIdAsync(descriptor.ClientId, cancellationToken);
        if (existing is null)
            await manager.CreateAsync(descriptor, cancellationToken);
        else
            await manager.UpdateAsync(existing, descriptor, cancellationToken);
    }

    private static bool IsAbsolute(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out _);
}
