using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Sports.Contracts;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Sports;

/// <summary>
/// Routine tools for the assistant (ADR 0011, ADR 0014, ADR 0015): the exercise library, daily routines, their
/// passes and ticks, through the same libraries and contract as the endpoints.
/// </summary>
[McpServerToolType]
public sealed class RoutineAssistantTools
{
    public const string MeasureHelp = "Mesure : hold (tenue, en secondes) ou reps (répétitions).";
    public const string LoadHelp = "Charge habituelle en kg, par pas de 0,5, proposée comme cible quand l'exercice est ajouté à " +
        "une routine ; vide ou 0 = au poids du corps.";
    public const string PassHelp = "Une routine peut être faite plusieurs fois le même jour : chaque fois est un passage. Un passage " +
        "est terminé quand chaque exercice a toutes ses séries cochées ; un passage terminé ne se rouvre pas (supprimez-le). Un seul " +
        "passage en cours à la fois par routine et par jour.";

    [McpServerTool(Name = "list_exercises", ReadOnly = true, Title = "Exercices")]
    [Description("Liste les exercices du foyer (archivés compris si demandé) : nom, consigne, mesure, charge habituelle " +
        "(defaultLoadKg, vide = au poids du corps). " + MeasureHelp)]
    public static async Task<List<ExerciseDto>> ListExercises(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        bool includeArchived = false, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await exercises.ListAsync(household.HouseholdId, includeArchived, ct);
    }

    [McpServerTool(Name = "create_exercise", Title = "Créer un exercice")]
    [Description("Crée un exercice dans la bibliothèque du foyer, par exemple « Chaise contre le mur », hold, 10 kg, avec " +
        "son image si vous en avez trouvé une. " + MeasureHelp)]
    public static async Task<ExerciseDto> CreateExercise(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        string name, string measure,
        [Description(LoadHelp)] decimal? defaultLoadKg = null,
        [Description("Consigne : position, points d'attention (2000 caractères maximum).")] string? instructions = null,
        [Description("Adresse https d'une image (JPEG, PNG ou WebP, 10 Mo maximum) à mettre sur l'exercice ; l'image " +
            "elle-même, pas la page qui la contient.")] string? imageUrl = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var exercise = await exercises.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new ExerciseRequest(name, instructions, measure, defaultLoadKg)), ct);
        if (string.IsNullOrWhiteSpace(imageUrl)) return exercise;
        return (await exercises.ReplaceImageFromUrlAsync(household.HouseholdId, exercise.Id, imageUrl, ct)).OrNotFound("Exercice");
    }

    [McpServerTool(Name = "update_exercise", Idempotent = true, Title = "Modifier un exercice")]
    [Description("Remplace tout un exercice (une consigne ou une charge omise devient vide). La mesure d'un exercice déjà réalisé " +
        "ne change pas. Les routines gardent leur propre charge. " + MeasureHelp)]
    public static async Task<ExerciseDto> UpdateExercise(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        Guid exerciseId, string name, string measure, [Description(LoadHelp)] decimal? defaultLoadKg = null, string? instructions = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new ExerciseRequest(name, instructions, measure, defaultLoadKg));
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
    [Description("Suivi d'un exercice pour un profil : record de valeur (secondes ou répétitions), record de charge, la " +
        "meilleure valeur et la meilleure charge de chaque semaine, toutes routines et tous passages confondus, et les dernières " +
        "coches (recent) avec les séries faites.")]
    public static async Task<ExerciseProgressDto> GetExerciseProgress(IHttpContextAccessor accessor, ExerciseLibrary exercises,
        Guid exerciseId, [Description("Nombre de semaines (1 à 104).")] int weeks = 12,
        [Description(SportAssistantTools.ProfileHelp)] Guid? profileId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await exercises.ProgressAsync(household.HouseholdId, exerciseId, profileId, weeks, ct)).OrNotFound("Exercice");
    }

    [McpServerTool(Name = "list_routines", ReadOnly = true, Title = "Routines")]
    [Description("Routines d'un profil avec leurs exercices et cibles, les passages du jour (todaySessions, avec leurs coches, " +
        "séries faites), le nombre de passages terminés (passesToday), la série en cours, le record et les jokers " +
        "restants cette semaine. Une routine est faite pour le jour dès qu'un passage est terminé ; les jours non prévus ne " +
        "comptent pas. " + PassHelp)]
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
            "loadKg (charge par pas de 0,5 ; la charge habituelle de l'exercice si omis, 0 = au poids du corps).")] RoutineLineRequest[] lines,
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
    [Description("Coche un exercice d'une routine (« j'ai fait la chaise, 4 séries de 45 s ») dans le passage en cours du jour, " +
        "ou dans un nouveau passage s'il n'y en a pas, et renvoie l'état de la routine. Corrige aussi ce qui a été noté dans un " +
        "passage (sessionId). Sans valeur, la cible du jour est enregistrée. " + PassHelp)]
    public static async Task<RoutineReportDto> CompleteExercise(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId, Guid exerciseId,
        [Description("Séries faites ; toutes les séries de la cible si omis.")] int? setsDone = null,
        [Description("Secondes ou répétitions par série réellement faites ; la cible si omis.")] int? value = null,
        [Description("Charge réellement utilisée en kg ; celle de la cible si omis, 0 = au poids du corps.")] decimal? loadKg = null,
        [Description("Jour (aaaa-MM-jj) ; aujourd'hui si omis, jamais dans le futur, seulement un jour prévu.")] DateOnly? date = null,
        [Description("Passage visé (todaySessions de list_routines) ; le passage en cours du jour si omis.")] Guid? sessionId = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new ExerciseCompletionRequest(exerciseId, setsDone, value, loadKg, date, sessionId));
        return (await routines.CompleteAsync(household.HouseholdId, routineId, request, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "uncomplete_exercise", Destructive = true, Idempotent = true, Title = "Décocher un exercice")]
    [Description("Décoche un exercice dans un passage en cours (par défaut le dernier passage du jour où il est coché) et renvoie " +
        "l'état de la routine. Refusé dans un passage terminé : supprimez le passage (delete_routine_session).")]
    public static async Task<RoutineReportDto> UncompleteExercise(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId, Guid exerciseId, Guid? sessionId = null, DateOnly? date = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.UncompleteAsync(household.HouseholdId, routineId, exerciseId, sessionId, date, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "log_routine_session", Title = "Enregistrer un passage")]
    [Description("Enregistre un passage entier d'une routine en un appel (« j'ai fait ma routine Boulot à 10 h ») : chaque exercice " +
        "fait avec ses séries, et en option sa valeur et sa charge. Les exercices omis ne sont pas cochés. " + PassHelp)]
    public static async Task<RoutineReportDto> LogRoutineSession(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId,
        [Description("Exercices faits : exerciseId, setsDone (toutes les séries si omis), value, loadKg (0 = au poids du corps).")] SessionExerciseRequest[] exercises,
        [Description("Jour (aaaa-MM-jj) ; aujourd'hui si omis.")] DateOnly? date = null,
        [Description("Heure de début (HH:mm, heure de Paris) ; maintenant, ou midi pour un jour passé, si omise.")] TimeOnly? time = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new RoutineSessionLogRequest(date, time, exercises));
        return (await routines.LogSessionAsync(household.HouseholdId, routineId, request, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "delete_routine_session", Destructive = true, Title = "Supprimer un passage")]
    [Description("Supprime un passage d'une routine avec toutes ses coches, par exemple pour refaire un passage terminé par erreur. " +
        "Demandez confirmation avant.")]
    public static async Task<RoutineReportDto> DeleteRoutineSession(IHttpContextAccessor accessor, RoutineLibrary routines,
        Guid routineId, [Description("Passage (todaySessions de list_routines).")] Guid sessionId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.DeleteSessionAsync(household.HouseholdId, routineId, sessionId, ct)).OrNotFound("Passage");
    }
}
