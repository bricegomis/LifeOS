using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Sports.Contracts;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Sports;

/// <summary>
/// Routine tools for the assistant (ADR 0011, ADR 0014): the exercise library, daily routines and
/// their ticks, through the same libraries and contract as the endpoints.
/// </summary>
[McpServerToolType]
public sealed class RoutineAssistantTools
{
    public const string MeasureHelp = "Mesure : hold (tenue, en secondes) ou reps (répétitions).";

    [McpServerTool(Name = "list_exercises", ReadOnly = true, Title = "Exercices")]
    [Description("Liste les exercices du foyer (archivés compris si demandé) : nom, consigne, mesure, avec ou sans charge. " + MeasureHelp)]
    public static async Task<List<ExerciseDto>> ListExercises(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        bool includeArchived = false, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await exercises.ListAsync(household.HouseholdId, includeArchived, ct);
    }

    [McpServerTool(Name = "create_exercise", Title = "Créer un exercice")]
    [Description("Crée un exercice dans la bibliothèque du foyer, par exemple « Chaise contre le mur », hold, avec charge. " + MeasureHelp)]
    public static async Task<ExerciseDto> CreateExercise(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        string name, string measure,
        [Description("L'exercice se fait avec une charge en kg.")] bool withLoad = false,
        [Description("Consigne : position, points d'attention (2000 caractères maximum).")] string? instructions = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await exercises.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new ExerciseRequest(name, instructions, measure, withLoad)), ct);
    }

    [McpServerTool(Name = "update_exercise", Idempotent = true, Title = "Modifier un exercice")]
    [Description("Remplace tout un exercice (une consigne omise devient vide). La mesure d'un exercice déjà réalisé ne change pas. " + MeasureHelp)]
    public static async Task<ExerciseDto> UpdateExercise(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        Guid exerciseId, string name, string measure, bool withLoad = false, string? instructions = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new ExerciseRequest(name, instructions, measure, withLoad));
        return (await exercises.UpdateAsync(household.HouseholdId, exerciseId, request, ct)).OrNotFound("Exercice");
    }

    [McpServerTool(Name = "archive_exercise", Destructive = true, Title = "Archiver un exercice")]
    [Description("Archive un exercice : il n'est plus proposé, son historique reste. Refusé tant qu'une routine en cours l'utilise.")]
    public static async Task<string> ArchiveExercise(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        Guid exerciseId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await exercises.ArchiveAsync(household.HouseholdId, exerciseId, ct)) throw AssistantRequest.NotFound("Exercice");
        return "Exercice archivé.";
    }

    [McpServerTool(Name = "set_exercise_image_from_url", Idempotent = true, Title = "Image d'un exercice")]
    [Description("Télécharge une image (JPEG, PNG ou WebP, 10 Mo maximum, URL https publique) et la met sur l'exercice, " +
        "à la place de l'image actuelle. Préférez un schéma ou une photo claire de la position, libre de droits si possible.")]
    public static async Task<ExerciseDto> SetExerciseImageFromUrl(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        Guid exerciseId, [Description("Adresse https de l'image elle-même (pas de la page qui la contient).")] string url,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await exercises.ReplaceImageFromUrlAsync(household.HouseholdId, exerciseId, url, ct)).OrNotFound("Exercice");
    }

    [McpServerTool(Name = "remove_exercise_image", Idempotent = true, Title = "Retirer l'image d'un exercice")]
    [Description("Retire l'image d'un exercice.")]
    public static async Task<ExerciseDto> RemoveExerciseImage(IHttpContextAccessor accessor, ExerciseLibrary exercises, Guid exerciseId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await exercises.RemoveImageAsync(household.HouseholdId, exerciseId, ct)).OrNotFound("Exercice");
    }

    [McpServerTool(Name = "get_exercise_progress", ReadOnly = true, Title = "Progression d'un exercice")]
    [Description("Suivi d'un exercice pour un profil : record de valeur (secondes ou répétitions), record de charge, et la " +
        "meilleure valeur et la meilleure charge de chaque semaine, toutes routines confondues.")]
    public static async Task<ExerciseProgressDto> GetExerciseProgress(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        Guid exerciseId, [Description("Nombre de semaines (1 à 104).")] int weeks = 12,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await exercises.ProgressAsync(household.HouseholdId, exerciseId, profileId, weeks, ct)).OrNotFound("Exercice");
    }

    [McpServerTool(Name = "list_routines", ReadOnly = true, Title = "Routines")]
    [Description("Routines d'un profil avec leurs exercices et cibles, ce qui est coché aujourd'hui (todayCompletions), la série " +
        "en cours, le record et les jokers restants cette semaine. Une routine est faite quand tous ses exercices du jour sont " +
        "cochés ; les jours non prévus ne comptent pas.")]
    public static async Task<IReadOnlyList<RoutineReportDto>> ListRoutines(IHttpContextAccessor accessor, SportProfiles profiles,
        SportReadContract contract,
        [Description("Semaines de calendrier jour par jour à joindre (0 à 53).")] int calendarWeeks = 0,
        bool includeStopped = false,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var profile = await profiles.ResolveAsync(household.HouseholdId, profileId, ct);
        return await contract.RoutinesAsync(household.HouseholdId, profile, calendarWeeks, includeStopped, ct);
    }

    [McpServerTool(Name = "create_routine", Title = "Créer une routine")]
    [Description("Crée une routine qui démarre aujourd'hui : une liste ordonnée d'exercices (list_exercises) avec leur cible, " +
        "par exemple chaise, 2 séries de 45 secondes à 10 kg.")]
    public static async Task<RoutineReportDto> CreateRoutine(IHttpContextAccessor accessor, RoutineLibrary routines,
        string name,
        [Description("Exercices dans l'ordre : exerciseId, sets (séries, 1 à 20), target (secondes ou répétitions par série), " +
            "loadKg (charge, par pas de 0,5, seulement pour un exercice avec charge).")] RoutineLineRequest[] lines,
        [Description("Jours de la semaine, 1 (lundi) à 7 (dimanche) ; tous les jours si omis.")] int[]? days = null,
        [Description("Jours prévus manqués tolérés par semaine sans casser la série (0 à 2).")] int jokersPerWeek = 0,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await routines.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new RoutineRequest(profileId, name, days, jokersPerWeek, lines)), ct);
    }

    [McpServerTool(Name = "update_routine", Idempotent = true, Title = "Modifier une routine")]
    [Description("Remplace toute une routine : nom, jours, jokers et liste complète des exercices avec leurs cibles. Les " +
        "changements valent à partir d'aujourd'hui ; les jours passés restent jugés avec la routine d'alors.")]
    public static async Task<RoutineReportDto> UpdateRoutine(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId, string name,
        [Description("Liste complète des exercices dans l'ordre : exerciseId, sets, target, loadKg.")] RoutineLineRequest[] lines,
        [Description("Jours de la semaine, 1 (lundi) à 7 (dimanche) ; tous les jours si omis.")] int[]? days = null,
        int jokersPerWeek = 0, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new RoutineRequest(null, name, days, jokersPerWeek, lines));
        return (await routines.UpdateAsync(household.HouseholdId, routineId, request, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "stop_routine", Destructive = true, Title = "Arrêter une routine")]
    [Description("Arrête une routine après aujourd'hui ; son historique est conservé. Demandez confirmation avant.")]
    public static async Task<string> StopRoutine(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await routines.StopAsync(household.HouseholdId, routineId, ct)) throw AssistantRequest.NotFound("Routine");
        return "Routine arrêtée.";
    }

    [McpServerTool(Name = "complete_exercise", Idempotent = true, Title = "Cocher un exercice")]
    [Description("Coche un exercice d'une routine (« j'ai fait la chaise 60 s à 12 kg »), ou corrige ce qui a été noté ce " +
        "jour-là, et renvoie l'état de la routine. Sans valeur, la cible du jour est enregistrée.")]
    public static async Task<RoutineReportDto> CompleteExercise(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId, Guid exerciseId,
        [Description("Secondes ou répétitions réellement faites ; la cible si omis.")] int? value = null,
        [Description("Charge réellement utilisée en kg ; celle de la cible si omis.")] decimal? loadKg = null,
        [Description("Jour (aaaa-MM-jj) ; aujourd'hui si omis, jamais dans le futur, seulement un jour prévu.")] DateOnly? date = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new ExerciseCompletionRequest(exerciseId, value, loadKg, date));
        return (await routines.CompleteAsync(household.HouseholdId, routineId, request, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "uncomplete_exercise", Destructive = true, Idempotent = true, Title = "Décocher un exercice")]
    [Description("Décoche un exercice d'une routine pour un jour (aujourd'hui si omis) et renvoie l'état de la routine.")]
    public static async Task<RoutineReportDto> UncompleteExercise(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId, Guid exerciseId, DateOnly? date = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.UncompleteAsync(household.HouseholdId, routineId, exerciseId, date, ct)).OrNotFound("Routine");
    }
}
