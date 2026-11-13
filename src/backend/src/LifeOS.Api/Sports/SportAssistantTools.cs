using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Sports.Contracts;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Sports;

/// <summary>
/// Sport tools for the assistant (ADR 0011, ADR 0013): templates, logged sessions and the week
/// report, through the same libraries and contract as the endpoints.
/// </summary>
[McpServerToolType]
public sealed class SportAssistantTools
{
    public const string SportHelp =
        "Sports (code obligatoire) : running (Course), walking (Marche), hiking (Randonnée), cycling (Vélo), " +
        "swimming (Natation), strength (Musculation), crossfit (Crossfit), bodyweight (Renforcement), mobility (Mobilité), " +
        "team_sport (Sport collectif), racket (Sport de raquette), other (Autre). Une distance n'est acceptée que pour " +
        "running, walking, hiking, cycling et swimming.";

    public const string ProfileHelp = "Profil concerné (get_household) ; le profil principal du foyer si omis.";

    [McpServerTool(Name = "get_sport_catalog", ReadOnly = true, Title = "Sports")]
    [Description("Liste les sports possibles et la date du jour du module Sport (Europe/Paris).")]
    public static SportCatalogDto GetSportCatalog(SportReadContract contract) => SportOverviewEndpoints.Catalog(contract.Today());

    [McpServerTool(Name = "list_sport_templates", ReadOnly = true, Title = "Séances types")]
    [Description("Liste les séances types du foyer (archivées comprises si demandé). Une séance type propose des valeurs " +
        "recopiées dans une séance réalisée.")]
    public static async Task<List<SportTemplateDto>> ListSportTemplates(IHttpContextAccessor accessor, SportTemplateLibrary templates,
        bool includeArchived = false, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await templates.ListAsync(household.HouseholdId, includeArchived, ct);
    }

    [McpServerTool(Name = "create_sport_template", Title = "Créer une séance type")]
    [Description("Crée une séance type du foyer. " + SportHelp + " Les calories sont celles que l'utilisateur donne ; " +
        "ne les calculez pas vous-même sans son accord.")]
    public static async Task<SportTemplateDto> CreateSportTemplate(IHttpContextAccessor accessor, SportTemplateLibrary templates,
        [Description("Nom, par exemple « Footing 45 min ».")] string name,
        string sport,
        [Description("Durée en minutes (1 à 1440).")] int durationMinutes,
        [Description("Calories proposées pour une séance (kcal).")] int calories,
        [Description("Distance en km, seulement pour un sport d'endurance.")] decimal? distanceKm = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new SportTemplateRequest(name, sport, durationMinutes, distanceKm, calories));
        return await templates.CreateAsync(household.HouseholdId, request, ct);
    }

    [McpServerTool(Name = "update_sport_template", Idempotent = true, Title = "Modifier une séance type")]
    [Description("Remplace toute une séance type (un champ omis devient vide). Les séances déjà enregistrées ne changent pas. " + SportHelp)]
    public static async Task<SportTemplateDto> UpdateSportTemplate(IHttpContextAccessor accessor, SportTemplateLibrary templates,
        Guid templateId, string name, string sport, int durationMinutes, int calories, decimal? distanceKm = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new SportTemplateRequest(name, sport, durationMinutes, distanceKm, calories));
        return (await templates.UpdateAsync(household.HouseholdId, templateId, request, ct)).OrNotFound("Séance type");
    }

    [McpServerTool(Name = "archive_sport_template", Destructive = true, Title = "Archiver une séance type")]
    [Description("Archive une séance type : elle n'est plus proposée, les séances passées gardent son nom.")]
    public static async Task<string> ArchiveSportTemplate(IHttpContextAccessor accessor, SportTemplateLibrary templates,
        Guid templateId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await templates.ArchiveAsync(household.HouseholdId, templateId, ct)) throw AssistantRequest.NotFound("Séance type");
        return "Séance type archivée.";
    }

    [McpServerTool(Name = "list_sport_sessions", ReadOnly = true, Title = "Séances réalisées")]
    [Description("Liste les séances réalisées d'un profil entre deux dates incluses (aaaa-MM-jj), les plus récentes d'abord.")]
    public static async Task<List<SportSessionDto>> ListSportSessions(IHttpContextAccessor accessor, SportSessionLibrary sessions,
        DateOnly from, DateOnly to, [Description(ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await sessions.ListAsync(household.HouseholdId, profileId, from, to, ct);
    }

    [McpServerTool(Name = "log_sport_session", Title = "Enregistrer une séance")]
    [Description("Enregistre une séance réalisée (jamais dans le futur). Partant d'une séance type, recopiez ses valeurs " +
        "et passez templateId pour qu'elle compte dans les objectifs par séance type. Calories : celles que l'utilisateur " +
        "donne ; inconnues, laissez vide (le total de la semaine sera partiel), ne les inventez pas. " + SportHelp)]
    public static async Task<SportSessionDto> LogSportSession(IHttpContextAccessor accessor, SportSessionLibrary sessions,
        string sport,
        [Description("Durée en minutes (1 à 1440).")] int durationMinutes,
        [Description("Jour de la séance (aaaa-MM-jj) ; aujourd'hui si omis.")] DateOnly? date = null,
        Guid? templateId = null,
        [Description("Distance en km, sports d'endurance seulement.")] decimal? distanceKm = null,
        [Description("Calories dépensées (kcal), si connues.")] int? calories = null,
        [Description("Ressenti de 1 (très dur) à 10 (excellent).")] int? feeling = null,
        [Description("Fréquence cardiaque moyenne (bpm).")] int? averageHeartRate = null,
        [Description("Fréquence cardiaque maximale (bpm).")] int? maxHeartRate = null,
        [Description("Texte libre : exercices, séries, charges, WOD, score.")] string? notes = null,
        [Description(ProfileHelp)] Guid? profileId = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new SportSessionRequest(profileId, date, sport, templateId, durationMinutes,
            distanceKm, calories, feeling, averageHeartRate, maxHeartRate, notes));
        return await sessions.CreateAsync(household.HouseholdId, request, ct);
    }

    [McpServerTool(Name = "update_sport_session", Idempotent = true, Title = "Modifier une séance")]
    [Description("Remplace toutes les valeurs d'une séance réalisée (un champ omis devient vide ; la date reste si omise). " + SportHelp)]
    public static async Task<SportSessionDto> UpdateSportSession(IHttpContextAccessor accessor, SportSessionLibrary sessions,
        Guid sessionId, string sport, int durationMinutes, DateOnly? date = null, Guid? templateId = null, decimal? distanceKm = null,
        int? calories = null, int? feeling = null, int? averageHeartRate = null, int? maxHeartRate = null, string? notes = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new SportSessionRequest(null, date, sport, templateId, durationMinutes,
            distanceKm, calories, feeling, averageHeartRate, maxHeartRate, notes));
        return (await sessions.UpdateAsync(household.HouseholdId, sessionId, request, ct)).OrNotFound("Séance");
    }

    [McpServerTool(Name = "delete_sport_session", Destructive = true, Title = "Supprimer une séance")]
    [Description("Supprime une séance réalisée. Demandez confirmation à l'utilisateur avant.")]
    public static async Task<string> DeleteSportSession(IHttpContextAccessor accessor, SportSessionLibrary sessions,
        Guid sessionId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await sessions.DeleteAsync(household.HouseholdId, sessionId, ct)) throw AssistantRequest.NotFound("Séance");
        return "Séance supprimée.";
    }

    [McpServerTool(Name = "get_sport_weeks", ReadOnly = true, Title = "Bilan des semaines")]
    [Description("Bilan des dernières semaines (lundi à dimanche) d'un profil : séances, durée, distance, calories " +
        "(caloriesComplete faux si des séances n'ont pas de calories) et avancement de chaque objectif de la semaine.")]
    public static async Task<IReadOnlyList<WeekReportDto>> GetSportWeeks(IHttpContextAccessor accessor, SportProfiles profiles,
        SportReadContract contract,
        [Description("Nombre de semaines, la semaine en cours comprise (1 à 104).")] int count = 1,
        [Description("Un jour de la dernière semaine voulue ; aujourd'hui si omis.")] DateOnly? until = null,
        [Description(ProfileHelp)] Guid? profileId = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var profile = await profiles.ResolveAsync(household.HouseholdId, profileId, ct);
        return await contract.WeeksAsync(household.HouseholdId, profile, until ?? contract.Today(), count, ct);
    }
}

/// <summary>Weekly goals and daily challenges for the assistant (ADR 0013).</summary>
[McpServerToolType]
public sealed class SportHabitAssistantTools
{
    private const string GoalHelp =
        "Forme (kind) : template (séances d'une séance type, templateId obligatoire), sport (séances d'un sport, sport " +
        "obligatoire), sessions (nombre de séances, tous sports) ou volume (unit obligatoire : kcal, km ou hours). " +
        "target : nombre de séances par semaine (entier 1 à 21) ou volume visé.";

    [McpServerTool(Name = "list_weekly_goals", ReadOnly = true, Title = "Objectifs de la semaine")]
    [Description("Liste les objectifs en cours d'un profil. Pour leur avancement, utilisez get_sport_weeks.")]
    public static async Task<List<WeeklyGoalDto>> ListWeeklyGoals(IHttpContextAccessor accessor, WeeklyGoalLibrary goals,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await goals.ListActiveAsync(household.HouseholdId, profileId, ct);
    }

    [McpServerTool(Name = "create_weekly_goal", Title = "Créer un objectif")]
    [Description("Crée un objectif de la semaine, compté à partir de la semaine en cours. " + GoalHelp + " " + SportAssistantTools.SportHelp)]
    public static async Task<WeeklyGoalDto> CreateWeeklyGoal(IHttpContextAccessor accessor, WeeklyGoalLibrary goals,
        string kind, decimal target, Guid? templateId = null, string? sport = null, string? unit = null,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await goals.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new WeeklyGoalRequest(profileId, kind, templateId, sport, unit, target)), ct);
    }

    [McpServerTool(Name = "change_weekly_goal", Idempotent = true, Title = "Modifier un objectif")]
    [Description("Modifie un objectif à partir de la semaine en cours ; les semaines passées gardent l'objectif d'alors " +
        "(l'identifiant renvoyé peut changer). " + GoalHelp)]
    public static async Task<WeeklyGoalDto> ChangeWeeklyGoal(IHttpContextAccessor accessor, WeeklyGoalLibrary goals,
        Guid goalId, string kind, decimal target, Guid? templateId = null, string? sport = null, string? unit = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new WeeklyGoalRequest(null, kind, templateId, sport, unit, target));
        return (await goals.ChangeAsync(household.HouseholdId, goalId, request, ct)).OrNotFound("Objectif");
    }

    [McpServerTool(Name = "stop_weekly_goal", Destructive = true, Title = "Arrêter un objectif")]
    [Description("Arrête un objectif : il compte encore cette semaine et disparaît des suivantes (un objectif créé cette semaine est supprimé).")]
    public static async Task<string> StopWeeklyGoal(IHttpContextAccessor accessor, WeeklyGoalLibrary goals, Guid goalId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await goals.StopAsync(household.HouseholdId, goalId, ct)) throw AssistantRequest.NotFound("Objectif");
        return "Objectif arrêté.";
    }

    [McpServerTool(Name = "list_daily_challenges", ReadOnly = true, Title = "Défis du jour")]
    [Description("Défis du jour d'un profil avec le total du jour, la cible, la série en cours, le record, les jokers restants " +
        "cette semaine, les totaux et le prochain palier. Un jour manqué consomme un joker de sa semaine s'il en reste.")]
    public static async Task<IReadOnlyList<ChallengeReportDto>> ListDailyChallenges(IHttpContextAccessor accessor, SportProfiles profiles,
        SportReadContract contract,
        [Description("Semaines de calendrier jour par jour à joindre (0 à 53).")] int calendarWeeks = 0,
        bool includeStopped = false,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var profile = await profiles.ResolveAsync(household.HouseholdId, profileId, ct);
        return await contract.ChallengesAsync(household.HouseholdId, profile, calendarWeeks, includeStopped, ct);
    }

    [McpServerTool(Name = "create_daily_challenge", Title = "Créer un défi")]
    [Description("Crée un défi quotidien qui démarre aujourd'hui, par exemple « Pompes », reps, 100 par jour, 1 joker par semaine.")]
    public static async Task<DailyChallengeDto> CreateDailyChallenge(IHttpContextAccessor accessor, DailyChallengeLibrary challenges,
        string name,
        [Description("reps (répétitions) ou minutes.")] string unit,
        [Description("Cible par jour.")] int dailyTarget,
        [Description("Jours manqués tolérés par semaine sans casser la série (0 à 2).")] int jokersPerWeek = 0,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await challenges.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new DailyChallengeRequest(profileId, name, unit, dailyTarget, jokersPerWeek)), ct);
    }

    [McpServerTool(Name = "update_daily_challenge", Idempotent = true, Title = "Modifier un défi")]
    [Description("Renomme un défi ; une nouvelle cible ou un nouveau nombre de jokers vaut à partir d'aujourd'hui. L'unité ne change pas.")]
    public static async Task<DailyChallengeDto> UpdateDailyChallenge(IHttpContextAccessor accessor, DailyChallengeLibrary challenges,
        Guid challengeId, string name, [Description("Unité actuelle du défi (reps ou minutes).")] string unit, int dailyTarget,
        int jokersPerWeek, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new DailyChallengeRequest(null, name, unit, dailyTarget, jokersPerWeek));
        return (await challenges.UpdateAsync(household.HouseholdId, challengeId, request, ct)).OrNotFound("Défi");
    }

    [McpServerTool(Name = "stop_daily_challenge", Destructive = true, Title = "Arrêter un défi")]
    [Description("Arrête un défi après aujourd'hui ; son historique est conservé. Demandez confirmation avant.")]
    public static async Task<string> StopDailyChallenge(IHttpContextAccessor accessor, DailyChallengeLibrary challenges,
        Guid challengeId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await challenges.StopAsync(household.HouseholdId, challengeId, ct)) throw AssistantRequest.NotFound("Défi");
        return "Défi arrêté.";
    }

    [McpServerTool(Name = "add_challenge_entry", Title = "Ajouter une série")]
    [Description("Ajoute une série à un défi (« j'ai fait 30 pompes ») et renvoie l'état du défi : total du jour, série, palier.")]
    public static async Task<ChallengeReportDto> AddChallengeEntry(IHttpContextAccessor accessor, DailyChallengeLibrary challenges,
        Guid challengeId,
        [Description("Répétitions ou minutes faites (1 à 10000).")] int quantity,
        [Description("Jour de la série (aaaa-MM-jj) ; aujourd'hui si omis, jamais dans le futur.")] DateOnly? date = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new ChallengeEntryRequest(quantity, date));
        return (await challenges.LogAsync(household.HouseholdId, challengeId, request, ct)).OrNotFound("Défi");
    }

    [McpServerTool(Name = "remove_challenge_entry", Destructive = true, Title = "Retirer une série")]
    [Description("Retire une série mal saisie (identifiant dans todayEntries de list_daily_challenges).")]
    public static async Task<ChallengeReportDto> RemoveChallengeEntry(IHttpContextAccessor accessor, DailyChallengeLibrary challenges,
        Guid challengeId, Guid entryId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await challenges.RemoveEntryAsync(household.HouseholdId, challengeId, entryId, ct)).OrNotFound("Série");
    }
}
