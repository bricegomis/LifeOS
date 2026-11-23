using System.ComponentModel;
using LifeOS.Api.Assistant;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Health;

/// <summary>Sleep and recovery imported from Garmin, for the assistant (ADR 0019). Read-only: the import writes it.</summary>
[McpServerToolType]
public sealed class WellnessAssistantTools
{
    [McpServerTool(Name = "get_sleep_and_recovery", ReadOnly = true, Title = "Sommeil et récupération")]
    [Description("Sommeil et récupération par jour, importés de Garmin : nuit (coucher, lever, durée, profond, léger, paradoxal, éveil " +
        "en secondes, score et qualité), FC au repos, HRV de la nuit avec sa moyenne de la semaine, sa plage habituelle et son statut, " +
        "respiration, SpO2, Body Battery (réveil, plus haut, plus bas), stress moyen et pas. 30 derniers jours si aucune période. " +
        HealthAssistantTools.PrivacyNote)]
    public static async Task<List<WellnessDto>> GetSleepAndRecovery(IHttpContextAccessor accessor, WellnessLibrary wellness,
        [Description("Premier jour (aaaa-MM-jj).")] DateOnly? from = null, [Description("Dernier jour (aaaa-MM-jj).")] DateOnly? to = null,
        CancellationToken ct = default) =>
        await wellness.ListAsync(await AssistantRequest.HouseholdAsync(accessor), from, to, ct);

    [McpServerTool(Name = "get_garmin_day_raw", ReadOnly = true, Title = "Données Garmin brutes d'un jour")]
    [Description("Les réponses brutes de Garmin pour un jour (JSON : sommeil, HRV, statistiques du jour), avec ce qui n'a pas de colonne " +
        "dans LifeOS : préparation et charge d'entraînement, SpO2 détaillée, etc. " + HealthAssistantTools.PrivacyNote)]
    public static async Task<string> GetGarminDayRaw(IHttpContextAccessor accessor, WellnessLibrary wellness,
        [Description("Le jour (aaaa-MM-jj).")] DateOnly date, CancellationToken ct = default) =>
        await wellness.RawAsync(await AssistantRequest.HouseholdAsync(accessor), date, ct) ?? throw AssistantRequest.NotFound("Jour Garmin");
}
