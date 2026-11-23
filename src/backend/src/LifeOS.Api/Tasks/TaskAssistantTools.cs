using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Tasks.Contracts;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Tasks;

/// <summary>
/// Tasks tools for the assistant (ADR 0011, ADR 0017): todos and their checklist, household routines and
/// their passes, projects, through the same libraries as the endpoints.
/// </summary>
[McpServerToolType]
public sealed class TaskAssistantTools
{
    public const string PriorityHelp = "Priorité : high, normal (par défaut) ou low.";
    public const string TagsHelp = "Étiquettes libres du foyer (maison, administratif, voiture…), 20 au plus ; réutilisez l'orthographe " +
        "de get_task_tags.";
    public const string ImageHelp = "Adresse https d'une image (JPEG, PNG ou WebP, 10 Mo maximum) ; l'image elle-même, pas la page " +
        "qui la contient. Sans image, la carte affiche l'initiale du titre.";
    public const string RecurrenceHelp = "Récurrence : kind = fixed (dates fixes) ou delay (N jours après le dernier passage, delayDays). " +
        "Pour fixed : unit = day (tous les every jours), week (days : 1 = lundi … 7 = dimanche, toutes les every semaines) ou month " +
        "(monthDay, tous les every mois ; le 31 tombe le dernier jour d'un mois plus court).";
    public const string LateHelp = "Un todo ouvert dont l'échéance est passée est en retard jusqu'à ce qu'il soit fait, reporté " +
        "(update_todo avec une nouvelle échéance) ou abandonné ; rien ne glisse tout seul.";

    // Todos

    [McpServerTool(Name = "get_tasks_today", ReadOnly = true, Title = "Tâches du jour")]
    [Description("La page Aujourd'hui des tâches : todos ouverts en retard, du jour et des 7 prochains jours, todos sans échéance, " +
        "todos faits aujourd'hui, et routines du foyer actives dues dans la semaine ou faites aujourd'hui. today = jour de Paris. " + LateHelp)]
    public static async Task<TaskTodayDto> GetTasksToday(IHttpContextAccessor accessor, TodoLibrary todos, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await todos.TodayAsync(household.HouseholdId, ct);
    }

    [McpServerTool(Name = "search_todos", ReadOnly = true, Title = "Chercher des todos")]
    [Description("Cherche les todos du foyer dans le titre, les notes, les étiquettes, les sous-tâches et le nom du projet, sans " +
        "tenir compte des accents ni des majuscules (chaque mot doit être trouvé). Triés par échéance, sans échéance ensuite, " +
        "clos à la fin.")]
    public static async Task<List<TodoDto>> SearchTodos(IHttpContextAccessor accessor, TodoLibrary todos,
        [Description("Texte cherché ; vide = tous.")] string? query = null,
        [Description("open (par défaut), done, abandoned ou all.")] string? state = null,
        string? tag = null, Guid? projectId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await todos.SearchAsync(household.HouseholdId, state, query, tag, ct, projectId);
    }

    [McpServerTool(Name = "get_todo", ReadOnly = true, Title = "Todo")]
    [Description("Un todo avec sa checklist.")]
    public static async Task<TodoDto> GetTodo(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.GetAsync(household.HouseholdId, todoId, ct)).OrNotFound("Todo");
    }

    [McpServerTool(Name = "get_task_tags", ReadOnly = true, Title = "Étiquettes des tâches")]
    [Description("Les étiquettes déjà utilisées par les todos et les routines du foyer.")]
    public static async Task<List<string>> GetTaskTags(IHttpContextAccessor accessor, TodoLibrary todos, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await todos.TagsAsync(household.HouseholdId, ct);
    }

    [McpServerTool(Name = "create_todo", Title = "Créer un todo")]
    [Description("Crée un todo (une chose à faire une fois) pour le profil principal, avec ses sous-tâches et son image si " +
        "vous en avez. " + PriorityHelp + " " + LateHelp)]
    public static async Task<TodoDto> CreateTodo(IHttpContextAccessor accessor, TodoLibrary todos,
        string title,
        [Description("Échéance (aaaa-mm-jj) ; vide = sans échéance.")] DateOnly? dueOn = null,
        [Description("Durée estimée en minutes, de 1 à 1440.")] int? durationMinutes = null,
        [Description(PriorityHelp)] string? priority = null,
        [Description(TagsHelp)] List<string>? tags = null,
        string? notes = null,
        [Description("Projet en cours du foyer (list_projects).")] Guid? projectId = null,
        [Description("Sous-tâches de la checklist, dans l'ordre.")] List<string>? checklist = null,
        [Description(ImageHelp)] string? imageUrl = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var todo = await todos.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new TodoRequest(null, title, notes, dueOn, durationMinutes, priority, tags, projectId)), ct);
        foreach (var label in checklist ?? [])
            todo = (await todos.AddChecklistItemAsync(household.HouseholdId, todo.Id, AssistantRequest.Validated(new ChecklistItemRequest(label)), ct))
                .OrNotFound("Todo");
        if (string.IsNullOrWhiteSpace(imageUrl)) return todo;
        return (await todos.ReplaceImageFromUrlAsync(household.HouseholdId, todo.Id, imageUrl, ct)).OrNotFound("Todo");
    }

    [McpServerTool(Name = "update_todo", Idempotent = true, Title = "Modifier un todo")]
    [Description("Remplace les champs d'un todo (un champ omis devient vide ; la checklist ne change pas). Reporter un todo, " +
        "c'est le modifier avec une nouvelle échéance. " + PriorityHelp)]
    public static async Task<TodoDto> UpdateTodo(IHttpContextAccessor accessor, TodoLibrary todos,
        Guid todoId, string title, DateOnly? dueOn = null, int? durationMinutes = null, string? priority = null,
        [Description(TagsHelp)] List<string>? tags = null, string? notes = null,
        [Description("Projet en cours du foyer, ou le projet actuel du todo ; vide = sans projet.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new TodoRequest(null, title, notes, dueOn, durationMinutes, priority, tags, projectId));
        return (await todos.UpdateAsync(household.HouseholdId, todoId, request, ct)).OrNotFound("Todo");
    }

    [McpServerTool(Name = "complete_todo", Title = "Todo fait")]
    [Description("Marque un todo ouvert comme fait, aujourd'hui ou un jour passé. Cocher toutes ses sous-tâches ne suffit pas.")]
    public static async Task<TodoDto> CompleteTodo(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId,
        [Description("Jour où il a été fait (aaaa-mm-jj) ; aujourd'hui par défaut, jamais dans le futur.")] DateOnly? doneOn = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.CompleteAsync(household.HouseholdId, todoId, doneOn, ct)).OrNotFound("Todo");
    }

    [McpServerTool(Name = "reopen_todo", Title = "Rouvrir un todo")]
    [Description("Rouvre un todo fait ou abandonné ; son échéance est gardée.")]
    public static async Task<TodoDto> ReopenTodo(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.ReopenAsync(household.HouseholdId, todoId, ct)).OrNotFound("Todo");
    }

    [McpServerTool(Name = "abandon_todo", Destructive = true, Title = "Abandonner un todo")]
    [Description("Abandonne un todo ouvert ; il peut être rouvert.")]
    public static async Task<TodoDto> AbandonTodo(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.AbandonAsync(household.HouseholdId, todoId, ct)).OrNotFound("Todo");
    }

    [McpServerTool(Name = "add_checklist_item", Title = "Ajouter une sous-tâche")]
    [Description("Ajoute une sous-tâche à la fin de la checklist d'un todo (50 au plus).")]
    public static async Task<TodoDto> AddChecklistItem(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId, string label,
        bool done = false, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.AddChecklistItemAsync(household.HouseholdId, todoId, AssistantRequest.Validated(new ChecklistItemRequest(label, done)), ct))
            .OrNotFound("Todo");
    }

    [McpServerTool(Name = "update_checklist_item", Idempotent = true, Title = "Modifier une sous-tâche")]
    [Description("Renomme ou coche une sous-tâche (itemId : id dans la checklist du todo).")]
    public static async Task<TodoDto> UpdateChecklistItem(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId, Guid itemId,
        string label, bool done, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.UpdateChecklistItemAsync(household.HouseholdId, todoId, itemId, AssistantRequest.Validated(new ChecklistItemRequest(label, done)), ct))
            .OrNotFound("Todo");
    }

    [McpServerTool(Name = "remove_checklist_item", Destructive = true, Title = "Retirer une sous-tâche")]
    [Description("Retire une sous-tâche de la checklist d'un todo.")]
    public static async Task<TodoDto> RemoveChecklistItem(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId, Guid itemId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.RemoveChecklistItemAsync(household.HouseholdId, todoId, itemId, ct)).OrNotFound("Todo");
    }

    [McpServerTool(Name = "set_todo_image_from_url", Idempotent = true, Title = "Image d'un todo")]
    [Description("Télécharge une image et la met sur le todo, à la place de l'image actuelle. " + ImageHelp)]
    public static async Task<TodoDto> SetTodoImageFromUrl(IHttpContextAccessor accessor, TodoLibrary todos, Guid todoId, string url,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await todos.ReplaceImageFromUrlAsync(household.HouseholdId, todoId, url, ct)).OrNotFound("Todo");
    }

    // Household routines

    [McpServerTool(Name = "list_task_routines", ReadOnly = true, Title = "Routines du foyer")]
    [Description("Les routines du foyer (sortir les poubelles, changer les draps…), à ne pas confondre avec les routines d'exercices " +
        "du sport : prochaine échéance (nextDueOn), retard, série, dernière fois. Cherche dans le titre, les notes et les étiquettes.")]
    public static async Task<List<TaskRoutineDto>> ListTaskRoutines(IHttpContextAccessor accessor, TaskRoutineLibrary routines,
        [Description("active (actives et en pause, par défaut), stopped ou all.")] string? state = null,
        string? query = null, string? tag = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await routines.SearchAsync(household.HouseholdId, state, query, tag, ct);
    }

    [McpServerTool(Name = "get_task_routine", ReadOnly = true, Title = "Routine du foyer")]
    [Description("Une routine du foyer avec ses passages, du plus récent au plus ancien.")]
    public static async Task<TaskRoutineDetailDto> GetTaskRoutine(IHttpContextAccessor accessor, TaskRoutineLibrary routines, Guid routineId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.GetAsync(household.HouseholdId, routineId, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "create_task_routine", Title = "Créer une routine du foyer")]
    [Description("Crée une routine du foyer pour le profil principal. Une seule échéance ouverte à la fois : en retard, les suivantes " +
        "ne s'empilent pas. " + RecurrenceHelp + " " + PriorityHelp)]
    public static async Task<TaskRoutineDetailDto> CreateTaskRoutine(IHttpContextAccessor accessor, TaskRoutineLibrary routines,
        string title, RecurrenceRequest recurrence,
        [Description("Première échéance possible (aaaa-mm-jj) ; aujourd'hui par défaut. Les dates fixes sont comptées depuis ce jour.")] DateOnly? startOn = null,
        int? durationMinutes = null, [Description(PriorityHelp)] string? priority = null, [Description(TagsHelp)] List<string>? tags = null,
        string? notes = null, [Description(ImageHelp)] string? imageUrl = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var routine = await routines.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new TaskRoutineRequest(null, title, notes, durationMinutes, priority, tags, recurrence, startOn)), ct);
        if (string.IsNullOrWhiteSpace(imageUrl)) return routine;
        return (await routines.ReplaceImageFromUrlAsync(household.HouseholdId, routine.Routine.Id, imageUrl, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "update_task_routine", Idempotent = true, Title = "Modifier une routine du foyer")]
    [Description("Remplace les champs d'une routine du foyer (un champ omis devient vide). Une récurrence modifiée vaut pour les " +
        "échéances suivantes ; l'historique ne change pas. " + RecurrenceHelp)]
    public static async Task<TaskRoutineDetailDto> UpdateTaskRoutine(IHttpContextAccessor accessor, TaskRoutineLibrary routines,
        Guid routineId, string title, RecurrenceRequest recurrence,
        [Description("Première échéance possible ; inchangée quand vide.")] DateOnly? startOn = null,
        int? durationMinutes = null, string? priority = null, [Description(TagsHelp)] List<string>? tags = null, string? notes = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new TaskRoutineRequest(null, title, notes, durationMinutes, priority, tags, recurrence, startOn));
        return (await routines.UpdateAsync(household.HouseholdId, routineId, request, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "complete_task_routine", Title = "Routine du foyer faite")]
    [Description("Clôt l'échéance ouverte d'une routine active : faite ce jour-là, avec une note facultative. Faite au plus tard le " +
        "jour de l'échéance, la série continue ; après, elle repart de zéro.")]
    public static async Task<TaskRoutineDetailDto> CompleteTaskRoutine(IHttpContextAccessor accessor, TaskRoutineLibrary routines, Guid routineId,
        [Description("Jour où elle a été faite ; aujourd'hui par défaut, jamais dans le futur ni avant le dernier passage.")] DateOnly? day = null,
        [Description("Note facultative (500 caractères maximum).")] string? note = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        AssistantRequest.Validated(new RoutineDoneRequest(day, note));
        return (await routines.CompleteAsync(household.HouseholdId, routineId, day, note, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "skip_task_routine", Title = "Sauter une routine du foyer")]
    [Description("Saute l'échéance ouverte d'une routine active ; la série repart de zéro.")]
    public static async Task<TaskRoutineDetailDto> SkipTaskRoutine(IHttpContextAccessor accessor, TaskRoutineLibrary routines, Guid routineId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.SkipAsync(household.HouseholdId, routineId, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "remove_task_routine_pass", Destructive = true, Title = "Supprimer un passage")]
    [Description("Supprime un passage (fait ou sauté) d'une routine du foyer ; l'échéance et la série sont recalculées.")]
    public static async Task<TaskRoutineDetailDto> RemoveTaskRoutinePass(IHttpContextAccessor accessor, TaskRoutineLibrary routines,
        Guid routineId, Guid passId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.RemovePassAsync(household.HouseholdId, routineId, passId, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "pause_task_routine", Title = "Mettre en pause une routine du foyer")]
    [Description("Met une routine du foyer en pause (plus d'échéance, série gardée) ou la reprend (resume = true) : à la reprise, " +
        "aucune échéance n'est antérieure au jour de la reprise.")]
    public static async Task<TaskRoutineDetailDto> PauseTaskRoutine(IHttpContextAccessor accessor, TaskRoutineLibrary routines, Guid routineId,
        bool resume = false, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var detail = resume
            ? await routines.ResumeAsync(household.HouseholdId, routineId, ct)
            : await routines.PauseAsync(household.HouseholdId, routineId, ct);
        return detail.OrNotFound("Routine");
    }

    [McpServerTool(Name = "stop_task_routine", Destructive = true, Title = "Arrêter une routine du foyer")]
    [Description("Arrête une routine du foyer pour de bon ; son historique reste.")]
    public static async Task<TaskRoutineDetailDto> StopTaskRoutine(IHttpContextAccessor accessor, TaskRoutineLibrary routines, Guid routineId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.StopAsync(household.HouseholdId, routineId, ct)).OrNotFound("Routine");
    }

    [McpServerTool(Name = "set_task_routine_image_from_url", Idempotent = true, Title = "Image d'une routine du foyer")]
    [Description("Télécharge une image et la met sur la routine du foyer. " + ImageHelp)]
    public static async Task<TaskRoutineDetailDto> SetTaskRoutineImageFromUrl(IHttpContextAccessor accessor, TaskRoutineLibrary routines,
        Guid routineId, string url, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await routines.ReplaceImageFromUrlAsync(household.HouseholdId, routineId, url, ct)).OrNotFound("Routine");
    }

    // Projects

    [McpServerTool(Name = "list_projects", ReadOnly = true, Title = "Projets")]
    [Description("Les projets du foyer avec leur avancement : done / total = todos faits sur todos non abandonnés, sous-projets " +
        "compris. parentId donne le projet parent d'un sous-projet (arborescence de profondeur libre).")]
    public static async Task<List<TaskProjectDto>> ListProjects(IHttpContextAccessor accessor, TaskProjectLibrary projects,
        [Description("active (en cours, par défaut), closed (terminés et abandonnés) ou all.")] string? state = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await projects.ListAsync(household.HouseholdId, state, ct);
    }

    [McpServerTool(Name = "get_project", ReadOnly = true, Title = "Projet")]
    [Description("Un projet avec ses propres todos, ses sous-projets directs et ses ancêtres (du projet racine au parent).")]
    public static async Task<TaskProjectDetailDto> GetProject(IHttpContextAccessor accessor, TaskProjectLibrary projects, Guid projectId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await projects.GetAsync(household.HouseholdId, projectId, ct)).OrNotFound("Projet");
    }

    [McpServerTool(Name = "create_project", Title = "Créer un projet")]
    [Description("Crée un projet (un simple regroupement de todos, sans étapes ni dépendances), éventuellement sous-projet d'un " +
        "autre. Ajoutez ensuite ses todos avec create_todo et projectId.")]
    public static async Task<TaskProjectDetailDto> CreateProject(IHttpContextAccessor accessor, TaskProjectLibrary projects,
        string title, DateOnly? dueOn = null, string? notes = null,
        [Description("Projet parent en cours du foyer (list_projects) ; vide = projet racine.")] Guid? parentId = null,
        [Description(ImageHelp)] string? imageUrl = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var project = await projects.CreateAsync(household.HouseholdId,
            AssistantRequest.Validated(new TaskProjectRequest(title, notes, dueOn, parentId)), ct);
        if (string.IsNullOrWhiteSpace(imageUrl)) return project;
        return (await projects.ReplaceImageFromUrlAsync(household.HouseholdId, project.Project.Id, imageUrl, ct)).OrNotFound("Projet");
    }

    [McpServerTool(Name = "update_project", Idempotent = true, Title = "Modifier un projet")]
    [Description("Remplace le nom, l'échéance, les notes et le projet parent d'un projet (un champ omis devient vide : sans " +
        "parentId, le projet remonte à la racine).")]
    public static async Task<TaskProjectDetailDto> UpdateProject(IHttpContextAccessor accessor, TaskProjectLibrary projects,
        Guid projectId, string title, DateOnly? dueOn = null, string? notes = null,
        [Description("Projet parent en cours du foyer, ou le parent actuel ; jamais le projet lui-même ni un de ses sous-projets.")]
        Guid? parentId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await projects.UpdateAsync(household.HouseholdId, projectId,
                AssistantRequest.Validated(new TaskProjectRequest(title, notes, dueOn, parentId)), ct))
            .OrNotFound("Projet");
    }

    [McpServerTool(Name = "close_project", Title = "Terminer, abandonner ou rouvrir un projet")]
    [Description("Termine (finish), abandonne (abandon) ou rouvre (reopen) un projet. Ses todos et ses sous-projets ne changent " +
        "jamais : les todos encore ouverts restent ouverts et gardent le projet ; dites-le à l'utilisateur.")]
    public static async Task<TaskProjectDetailDto> CloseProject(IHttpContextAccessor accessor, TaskProjectLibrary projects, Guid projectId,
        [Description("finish, abandon ou reopen.")] string action, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var detail = action?.Trim() switch
        {
            "finish" => await projects.FinishAsync(household.HouseholdId, projectId, ct),
            "abandon" => await projects.AbandonAsync(household.HouseholdId, projectId, ct),
            "reopen" => await projects.ReopenAsync(household.HouseholdId, projectId, ct),
            _ => throw new ArgumentException("Action : finish, abandon ou reopen."),
        };
        return detail.OrNotFound("Projet");
    }

    [McpServerTool(Name = "set_project_image_from_url", Idempotent = true, Title = "Image d'un projet")]
    [Description("Télécharge une image et la met sur le projet. " + ImageHelp)]
    public static async Task<TaskProjectDetailDto> SetProjectImageFromUrl(IHttpContextAccessor accessor, TaskProjectLibrary projects,
        Guid projectId, string url, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await projects.ReplaceImageFromUrlAsync(household.HouseholdId, projectId, url, ct)).OrNotFound("Projet");
    }
}
