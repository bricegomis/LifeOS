using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LifeOS.Api.Authentication;

/// <summary>An assistant (Claude) the user allowed to act on LifeOS.</summary>
public sealed record AssistantConnectionDto(string Id, string ClientName, DateTimeOffset? GrantedAt);

/// <summary>
/// « Assistants connectés » in Réglages (ADR 0011): the user's valid grants to other clients
/// than the LifeOS app. Revoking one revokes its tokens; the assistant must ask consent again.
/// </summary>
public static class ConnectionsEndpoints
{
    public static void MapConnectionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account/connections").WithTags("Account").RequireAuthorization();
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<AssistantConnectionDto>>();
        group.MapDelete("/{id}", RevokeAsync);
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictApplicationManager applications,
        CancellationToken ct)
    {
        if (!context.User.TryGetUserId(out var userId)) return Results.Unauthorized();
        var connections = new List<AssistantConnectionDto>();
        await foreach (var authorization in authorizations.FindBySubjectAsync(userId.ToString(), ct))
        {
            if (!await authorizations.HasStatusAsync(authorization, Statuses.Valid, ct)
                || !await authorizations.HasTypeAsync(authorization, AuthorizationTypes.Permanent, ct)) continue;
            var applicationId = await authorizations.GetApplicationIdAsync(authorization, ct);
            var application = applicationId is null ? null : await applications.FindByIdAsync(applicationId, ct);
            if (application is null) continue;
            var clientId = await applications.GetClientIdAsync(application, ct);
            if (clientId == AuthOptions.WebClientOptions.ClientId) continue;
            connections.Add(new AssistantConnectionDto(
                (await authorizations.GetIdAsync(authorization, ct))!,
                await applications.GetDisplayNameAsync(application, ct) ?? clientId ?? "Client inconnu",
                await authorizations.GetCreationDateAsync(authorization, ct)));
        }
        return Results.Ok(connections.OrderByDescending(connection => connection.GrantedAt).ToList());
    }

    private static async Task<IResult> RevokeAsync(
        string id,
        HttpContext context,
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictTokenManager tokens,
        CancellationToken ct)
    {
        if (!context.User.TryGetUserId(out var userId)) return Results.Unauthorized();
        if (!Guid.TryParse(id, out _)) return Results.NotFound();
        var authorization = await authorizations.FindByIdAsync(id, ct);
        // Someone else's grant answers like a missing one.
        if (authorization is null || await authorizations.GetSubjectAsync(authorization, ct) != userId.ToString())
            return Results.NotFound();
        await authorizations.TryRevokeAsync(authorization, ct);
        await tokens.RevokeByAuthorizationIdAsync(id, ct);
        return Results.NoContent();
    }
}
