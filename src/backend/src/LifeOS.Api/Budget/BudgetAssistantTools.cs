using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Budget;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Budget;

/// <summary>
/// Budget tools for the assistant (ADR 0011, ADR 0020), through the same library as the endpoints. They act with the account's
/// rights: only the caller's own budget. Statements are imported in the app; the assistant creates lines and rules and
/// classifies what rules cannot (PayPal, Amazon, transfers to people), for instance from confirmation emails.
/// </summary>
[McpServerToolType]
public sealed class BudgetAssistantTools
{
    public const string PrivacyNote = "Budget privé au compte connecté (un autre membre du foyer ne le voit pas).";
    public const string KindHelp = "Type : fixed (montant attendu : loyer, crédit, abonnement), envelope (plafond du mois, remis à zéro chaque mois), " +
        "extra (aucun montant prévu, pour suivre le non-planifié), off (hors budget : virement entre ses comptes, avance remboursée) ou income (revenu).";
    public const string PeriodHelp = "Période du montant : month (par mois, par défaut), year (par an) ou months (tous les N mois, avec months). Un revenu est par mois.";
    public const string MonthHelp = "Mois au format aaaa-mm ; le mois en cours par défaut.";

    [McpServerTool(Name = "list_budget_lines", ReadOnly = true, Title = "Lignes du budget")]
    [Description("Les lignes du budget (postes, type, montant prévu, à diminuer) et les postes existants. " + PrivacyNote)]
    public static async Task<BudgetLineListDto> ListBudgetLines(IHttpContextAccessor accessor, BudgetLibrary library,
        [Description("true : inclut les lignes arrêtées.")] bool includeStopped = false, CancellationToken ct = default) =>
        await library.ListLinesAsync(await AssistantRequest.HouseholdAsync(accessor), includeStopped, ct);

    [McpServerTool(Name = "get_budget_line", ReadOnly = true, Title = "Ligne du budget")]
    [Description("Une ligne avec son prévu et son réel sur 12 mois et ses règles de classement.")]
    public static async Task<BudgetLineDetailDto> GetBudgetLine(IHttpContextAccessor accessor, BudgetLibrary library, Guid lineId, CancellationToken ct = default) =>
        (await library.GetLineAsync(await AssistantRequest.HouseholdAsync(accessor), lineId, ct)).OrNotFound("Ligne");

    [McpServerTool(Name = "create_budget_line", Title = "Créer une ligne du budget")]
    [Description("Ajoute une ligne au budget, par exemple depuis le fichier Excel de l'utilisateur. Le montant compte depuis toujours. " + KindHelp + " " + PeriodHelp)]
    public static async Task<BudgetLineDto> CreateBudgetLine(IHttpContextAccessor accessor, BudgetLibrary library, string name,
        [Description(KindHelp)] string kind,
        [Description("Poste (Logement, Alimentation, Transport…) ; ignoré pour un revenu ou une ligne hors budget.")] string? category = null,
        [Description("Montant prévu en euros (fixed, envelope, income).")] decimal? amount = null,
        [Description(PeriodHelp)] string? period = null,
        [Description("Pour period = months : tous les combien de mois.")] int? months = null,
        [Description("true : ligne à diminuer, suivie dans l'évolution.")] bool reduce = false,
        [Description("true : le prévu est le coût par mois des compléments, calculé par la Santé (fixed ou envelope).")] bool fromSupplements = false,
        string? notes = null, CancellationToken ct = default) =>
        await library.CreateLineAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new BudgetLineRequest(name, category, kind, reduce, fromSupplements, notes, amount, period, months)), ct);

    [McpServerTool(Name = "update_budget_line", Idempotent = true, Title = "Modifier une ligne du budget")]
    [Description("Modifie une ligne ; un champ omis reste inchangé. Un nouveau montant vaut à partir de fromMonth (le mois en cours par défaut) ; " +
        "les mois d'avant gardent leur prévu. " + KindHelp + " " + PeriodHelp)]
    public static async Task<BudgetLineDto> UpdateBudgetLine(IHttpContextAccessor accessor, BudgetLibrary library, Guid lineId,
        string? name = null, string? kind = null, string? category = null, decimal? amount = null, string? period = null, int? months = null,
        bool? reduce = null, bool? fromSupplements = null, string? notes = null,
        [Description(MonthHelp)] string? fromMonth = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var line = (await library.GetLineAsync(household, lineId, ct)).OrNotFound("Ligne").Line;
        var request = new BudgetLineRequest(name ?? line.Name, category ?? line.Category, kind ?? line.Kind, reduce ?? line.Reduce,
            fromSupplements ?? line.FromSupplements, notes ?? line.Notes, amount, amount is null ? null : period ?? line.Amount?.Period,
            amount is null ? null : months ?? line.Amount?.Months, fromMonth);
        return (await library.UpdateLineAsync(household, lineId, AssistantRequest.Validated(request), ct)).OrNotFound("Ligne");
    }

    [McpServerTool(Name = "stop_budget_line", Title = "Arrêter une ligne du budget")]
    [Description("Arrête une ligne à partir d'un mois (elle ne compte plus) ; son historique reste.")]
    public static async Task<BudgetLineDto> StopBudgetLine(IHttpContextAccessor accessor, BudgetLibrary library, Guid lineId,
        [Description(MonthHelp)] string? fromMonth = null, CancellationToken ct = default) =>
        (await library.StopLineAsync(await AssistantRequest.HouseholdAsync(accessor), lineId, fromMonth, ct)).OrNotFound("Ligne");

    [McpServerTool(Name = "get_budget_month", ReadOnly = true, Title = "Budget du mois")]
    [Description("Le mois : revenus, dépenses et reste (réel et prévu), extras, enveloppes, postes et lignes avec prévu, réel et écart, " +
        "opérations non classées. Les opérations étalées comptent pour leur part de chaque mois.")]
    public static async Task<BudgetMonthDto> GetBudgetMonth(IHttpContextAccessor accessor, BudgetLibrary library,
        [Description(MonthHelp)] string? month = null, CancellationToken ct = default) =>
        await library.MonthAsync(await AssistantRequest.HouseholdAsync(accessor), month, ct);

    [McpServerTool(Name = "get_budget_trend", ReadOnly = true, Title = "Évolution du budget")]
    [Description("Dépenses par mois et par poste, extras, revenus, et le réel des lignes à diminuer, sur plusieurs mois.")]
    public static async Task<BudgetTrendDto> GetBudgetTrend(IHttpContextAccessor accessor, BudgetLibrary library,
        [Description("Nombre de mois (2 à 36), 6 par défaut.")] int months = 6, [Description("Dernier mois, " + MonthHelp)] string? until = null,
        CancellationToken ct = default) =>
        await library.TrendAsync(await AssistantRequest.HouseholdAsync(accessor), months, until, ct);

    [McpServerTool(Name = "list_budget_operations", ReadOnly = true, Title = "Opérations du budget")]
    [Description("Les opérations d'un mois (relevés importés et saisies), avec leur ligne. filter : all, unclassified (à ranger) ou spread " +
        "(achats étalés qui comptent dans le mois). Le libellé bancaire commence par CARTE, PRLV SEPA, VIR…")]
    public static async Task<BudgetOperationListDto> ListBudgetOperations(IHttpContextAccessor accessor, BudgetLibrary library,
        [Description(MonthHelp)] string? month = null, string filter = "all",
        [Description("Texte cherché dans les libellés, sans majuscules ni accents.")] string? query = null, CancellationToken ct = default) =>
        await library.ListOperationsAsync(await AssistantRequest.HouseholdAsync(accessor), month, filter, query, ct);

    [McpServerTool(Name = "classify_budget_operation", Idempotent = true, Title = "Ranger une opération")]
    [Description("Range une opération dans une ligne, avec un étalement facultatif (un achat qui couvre plusieurs mois). ruleText crée aussi la règle " +
        "« libellé contient … » vers la même ligne, appliquée aux autres opérations non classées : à n'utiliser que pour un libellé qui désigne " +
        "toujours la même dépense (pas PayPal, Amazon ni un virement à une personne). lineId absent : l'opération redevient non classée.")]
    public static async Task<BudgetClassifyResultDto> ClassifyBudgetOperation(IHttpContextAccessor accessor, BudgetLibrary library, Guid operationId,
        Guid? lineId = null, [Description("Mois couverts, de 1 à 36.")] int spread = 1, string? ruleText = null, CancellationToken ct = default)
    {
        var r = (await library.ClassifyAsync(await AssistantRequest.HouseholdAsync(accessor), operationId,
            AssistantRequest.Validated(new BudgetClassifyRequest(lineId, spread, ruleText)), "assistant", ct)) ?? throw AssistantRequest.NotFound("Opération");
        return new BudgetClassifyResultDto(r.Operation, r.Rule, r.Classified);
    }

    [McpServerTool(Name = "add_budget_operation", Title = "Saisir une opération")]
    [Description("Ajoute une opération absente des relevés (espèces). Montant négatif pour une dépense, positif pour un revenu ou un remboursement.")]
    public static async Task<BudgetOperationDto> AddBudgetOperation(IHttpContextAccessor accessor, BudgetLibrary library,
        [Description("Jour (aaaa-mm-jj).")] DateOnly date, string label, decimal amount, Guid? lineId = null,
        [Description("Mois couverts, de 1 à 36.")] int spread = 1, CancellationToken ct = default) =>
        await library.AddOperationAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new BudgetManualOperationRequest(date, label, amount, lineId, spread)), "assistant", ct);

    [McpServerTool(Name = "list_budget_rules", ReadOnly = true, Title = "Règles de classement")]
    [Description("Les règles dans leur ordre : la première qui correspond range l'opération importée. matches : opérations qu'elle range en premier.")]
    public static async Task<IReadOnlyList<BudgetRuleDto>> ListBudgetRules(IHttpContextAccessor accessor, BudgetLibrary library, CancellationToken ct = default) =>
        await library.ListRulesAsync(await AssistantRequest.HouseholdAsync(accessor), ct);

    [McpServerTool(Name = "create_budget_rule", Title = "Créer une règle de classement")]
    [Description("Règle « libellé contient … » (sans majuscules ni accents) vers une ligne ; elle range tout de suite les opérations non classées " +
        "qui correspondent, puis les prochains imports. direction : any, out (dépenses) ou in (revenus). position : place dans l'ordre (0 = d'abord).")]
    public static async Task<BudgetRuleResultDto> CreateBudgetRule(IHttpContextAccessor accessor, BudgetLibrary library, string text, Guid lineId,
        string direction = "any", int spread = 1, int? position = null, CancellationToken ct = default) =>
        await library.CreateRuleAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new BudgetRuleRequest(text, lineId, direction, spread, position)), ct);

    [McpServerTool(Name = "update_budget_rule", Idempotent = true, Title = "Modifier une règle de classement")]
    [Description("Modifie une règle ; un champ omis reste inchangé. Les opérations déjà rangées ne changent pas.")]
    public static async Task<BudgetRuleResultDto> UpdateBudgetRule(IHttpContextAccessor accessor, BudgetLibrary library, Guid ruleId,
        string? text = null, Guid? lineId = null, string? direction = null, int? spread = null, int? position = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var rule = (await library.ListRulesAsync(household, ct)).SingleOrDefault(r => r.Id == ruleId) ?? throw AssistantRequest.NotFound("Règle");
        return (await library.UpdateRuleAsync(household, ruleId, AssistantRequest.Validated(new BudgetRuleRequest(text ?? rule.Text, lineId ?? rule.LineId,
            direction ?? rule.Direction, spread ?? rule.Spread, position)), ct)).OrNotFound("Règle");
    }

    [McpServerTool(Name = "delete_budget_rule", Destructive = true, Title = "Supprimer une règle de classement")]
    [Description("Supprime une règle ; les opérations qu'elle a rangées restent dans leur ligne.")]
    public static async Task<string> DeleteBudgetRule(IHttpContextAccessor accessor, BudgetLibrary library, Guid ruleId, CancellationToken ct = default) =>
        await library.DeleteRuleAsync(await AssistantRequest.HouseholdAsync(accessor), ruleId, ct) ? "Règle supprimée." : throw AssistantRequest.NotFound("Règle");
}
