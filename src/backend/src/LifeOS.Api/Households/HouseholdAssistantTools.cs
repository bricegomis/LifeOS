using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Households;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Households;

public sealed record AssistantHousehold(
    string Name,
    [property: Description("Profils du foyer : chaque avis et chaque portion se rattache à un profil.")]
    IReadOnlyList<AssistantProfile> Profiles);

public sealed record AssistantProfile(
    Guid Id,
    string Name,
    [property: Description("Part d'une portion standard que mange ce profil (1 = une portion).")]
    decimal PortionCoefficient);

/// <summary>Household core tools for the assistant (ADR 0011).</summary>
[McpServerToolType]
public sealed class HouseholdAssistantTools
{
    [McpServerTool(Name = "get_household", ReadOnly = true, Title = "Foyer")]
    [Description("Renvoie le foyer LifeOS de l'utilisateur connecté et ses profils (nom, coefficient de portion). " +
        "À appeler en premier pour connaître les profils avant de lire ou d'écrire des avis.")]
    public static async Task<AssistantHousehold> GetHousehold(
        IHttpContextAccessor accessor,
        GetCurrentHouseholdQuery query,
        CancellationToken cancellationToken)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var current = await query.ExecuteAsync(household.HouseholdId, cancellationToken);
        return new AssistantHousehold(
            current.Name,
            current.MemberProfiles.Select(profile => new AssistantProfile(profile.Id, profile.DisplayName, profile.PortionCoefficient)).ToList());
    }
}

/// <summary>The Garmin connection of the account (ADR 0019), for the assistant.</summary>
[McpServerToolType]
public sealed class GarminAssistantTools
{
    [McpServerTool(Name = "get_garmin_status", ReadOnly = true, Title = "Connexion Garmin")]
    [Description("État de la synchro Garmin du compte : connecté, code de double authentification demandé, code refusé ou expiré, " +
        "identifiants refusés ou panne ; dernière synchro réussie et son résumé, dernière erreur, prochaine synchro. Le code Garmin se " +
        "saisit dans LifeOS (Sport › Garmin), jamais dans la discussion.")]
    public static async Task<GarminStatusDto> GetGarminStatus(IHttpContextAccessor accessor, GarminLinks links, CancellationToken ct = default) =>
        await links.StatusAsync(await LifeOS.Api.Assistant.AssistantRequest.HouseholdAsync(accessor), ct);

    [McpServerTool(Name = "request_garmin_sync", Title = "Synchroniser Garmin")]
    [Description("Demande une synchro Garmin maintenant : le service la prend dans les 5 minutes.")]
    public static async Task<GarminStatusDto> RequestGarminSync(IHttpContextAccessor accessor, GarminLinks links, CancellationToken ct = default) =>
        await links.ChangeAsync(await LifeOS.Api.Assistant.AssistantRequest.HouseholdAsync(accessor), (link, now) => link.RequestSync(now), ct);
}
