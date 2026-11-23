using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Sports.Contracts;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Sports;

/// <summary>Garmin data of the Sport module for the assistant (ADR 0019): read-only, the import writes it.</summary>
[McpServerToolType]
public sealed class GarminSportAssistantTools
{
    [McpServerTool(Name = "get_sport_session", ReadOnly = true, Title = "Une séance")]
    [Description("Une séance réalisée avec toutes ses valeurs : heures de début et de fin, durée active, distance, dénivelé, vitesse, " +
        "fréquence cardiaque, temps par zone de FC (secondes, zones 1 à 5), effort perçu (1 très facile à 10 maximal), sensation, " +
        "Training Effect, source (manual ou garmin) et notes.")]
    public static async Task<SportSessionDto> GetSportSession(IHttpContextAccessor accessor, SportSessionLibrary sessions, Guid sessionId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await sessions.GetAsync(household.HouseholdId, sessionId, ct)).OrNotFound("Séance");
    }

    [McpServerTool(Name = "list_vo2max", ReadOnly = true, Title = "VO2max")]
    [Description("VO2max estimée par la montre Garmin, une valeur par jour et par sport (running, cycling ; le vélo demande un capteur " +
        "de puissance), en ml/kg/min. Deux dernières années si aucune période n'est donnée.")]
    public static async Task<List<Vo2MaxDto>> ListVo2Max(IHttpContextAccessor accessor, GarminSportImport import,
        [Description("Premier jour (aaaa-MM-jj).")] DateOnly? from = null, [Description("Dernier jour (aaaa-MM-jj).")] DateOnly? to = null,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await import.Vo2MaxAsync(household.HouseholdId, profileId, from, to, ct);
    }
}
