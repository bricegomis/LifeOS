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
