using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Application.Budget;
using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Budget;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Budget;

/// <param name="Kind">fixed, envelope, extra, off or income.</param>
/// <param name="Category">Poste; ignored for income ("Revenus") and off ("Hors budget").</param>
/// <param name="FromSupplements">The planned amount is the monthly cost of the account's supplements, computed by Health.</param>
/// <param name="Amount">Planned amount, for fixed, envelope and income lines that do not follow the supplements.</param>
/// <param name="Period">month, year or months (every <paramref name="Months"/> months); an income is per month.</param>
/// <param name="FromMonth">aaaa-mm: the amount applies from this month on (earlier months keep theirs). On creation, from the start.</param>
public sealed record BudgetLineRequest(
    [property: Required, StringLength(BudgetLine.MaxNameLength)] string Name,
    [property: StringLength(BudgetLine.MaxCategoryLength)] string? Category,
    [property: Required] string Kind,
    bool Reduce = false,
    bool FromSupplements = false,
    [property: StringLength(BudgetLine.MaxNotesLength)] string? Notes = null,
    decimal? Amount = null,
    string? Period = null,
    int? Months = null,
    string? FromMonth = null);

/// <param name="FromMonth">aaaa-mm: first month the line no longer counts; the current month when omitted.</param>
public sealed record BudgetStopRequest(string? FromMonth);

/// <param name="Amount">Negative for an expense, positive for an income or a refund.</param>
/// <param name="Spread">Months the operation covers (1 to 36), from its month.</param>
public sealed record BudgetManualOperationRequest(DateOnly Date, [property: Required, StringLength(BudgetOperation.MaxLabelLength)] string Label,
    decimal Amount, Guid? LineId = null, int Spread = 1);

/// <param name="LineId">The line; null puts the operation back to unclassified.</param>
/// <param name="RuleText">When given, also creates the rule "label contains …" towards the same line (last in order) and applies it to the
/// other unclassified operations.</param>
public sealed record BudgetClassifyRequest(Guid? LineId, int Spread = 1, [property: StringLength(BudgetRule.MaxTextLength)] string? RuleText = null);

/// <param name="Direction">any, out (expenses only) or in (incomes only).</param>
/// <param name="Position">Place in the order, from 0; last when omitted.</param>
public sealed record BudgetRuleRequest([property: Required, StringLength(BudgetRule.MaxTextLength)] string Text, Guid LineId, string? Direction = null,
    int Spread = 1, int? Position = null);

public sealed record BudgetRuleMoveRequest(int Position);

/// <summary>Whose budget it is (ADR 0020): the caller's account, in its household, on the household's main profile.</summary>
public sealed record BudgetOwner(Guid HouseholdId, Guid UserId, Guid ProfileId);

/// <summary>
/// The caller's budget (ADR 0020), shared by the endpoints and the assistant tools. Every query filters on the household
/// and the account. The monthly cost of supplements comes from the Health read contract, never from health tables.
/// A null result means "not one of the caller's rows".
/// </summary>
public sealed class BudgetLibrary(LifeOSDbContext db, HealthReadContract health, TimeProvider clock)
{
    private const int HistoryMonths = 12;
    private static readonly DateOnly Beginning = new(2000, 1, 1);

    public DateOnly Today => BudgetClock.Today(clock);
    private DateOnly CurrentMonth => BudgetMonths.Of(Today);

    private async Task<BudgetOwner> OwnerAsync(HouseholdContext household, CancellationToken ct)
    {
        var profile = await db.MemberProfiles.Where(p => p.HouseholdId == household.HouseholdId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("Aucun profil dans votre foyer.");
        return new BudgetOwner(household.HouseholdId, household.UserId, profile);
    }

    private Task<decimal> SupplementsCostAsync(HouseholdContext household, CancellationToken ct) =>
        health.SupplementsMonthlyCostAsync(household.HouseholdId, household.UserId, ct);

    private IQueryable<BudgetLine> Lines(HouseholdContext h) => db.BudgetLines.Where(l => l.HouseholdId == h.HouseholdId && l.OwnerUserId == h.UserId);
    private IQueryable<BudgetOperation> Operations(HouseholdContext h) => db.BudgetOperations.Where(o => o.HouseholdId == h.HouseholdId && o.OwnerUserId == h.UserId);
    private IQueryable<BudgetRule> Rules(HouseholdContext h) => db.BudgetRules.Where(r => r.HouseholdId == h.HouseholdId && r.OwnerUserId == h.UserId);

    private static DateOnly MonthOr(string? text, DateOnly fallback) => string.IsNullOrWhiteSpace(text) ? fallback : BudgetMonths.Parse(text);

    // Lines

    public async Task<BudgetLineListDto> ListLinesAsync(HouseholdContext household, bool includeStopped, CancellationToken ct)
    {
        var month = CurrentMonth;
        var cost = await SupplementsCostAsync(household, ct);
        var lines = await Lines(household).ToListAsync(ct);
        var categories = BudgetLine.DefaultCategories.Concat(lines.Where(l => l.IsExpense).Select(l => l.Category))
            .Distinct(StringComparer.CurrentCultureIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        var dtos = lines.Where(l => includeStopped || l.ActiveIn(month))
            .OrderBy(l => l.Kind switch { "income" => 1, "off" => 2, _ => 0 }).ThenBy(l => l.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(l => BudgetLineDto.From(l, month, cost)).ToList();
        return new BudgetLineListDto(BudgetMonths.Format(month), cost, categories, dtos);
    }

    public async Task<BudgetLineDetailDto?> GetLineAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var line = await Lines(household).SingleOrDefaultAsync(l => l.Id == id, ct);
        if (line is null) return null;
        var cost = await SupplementsCostAsync(household, ct);
        var from = CurrentMonth.AddMonths(-HistoryMonths + 1);
        var operations = await Operations(household).Where(o => o.LineId == id && o.Date >= from.AddMonths(-BudgetOperation.MaxSpread)).ToListAsync(ct);
        var rules = await RuleDtosAsync(household, ct);
        return new BudgetLineDetailDto(BudgetLineDto.From(line, CurrentMonth, cost),
            BudgetReports.LineMonths(line, operations, CurrentMonth, HistoryMonths, cost), rules.Where(r => r.LineId == id).ToList());
    }

    public async Task<BudgetLineDto> CreateLineAsync(HouseholdContext household, BudgetLineRequest r, CancellationToken ct)
    {
        var owner = await OwnerAsync(household, ct);
        var now = clock.GetUtcNow();
        var line = BudgetLine.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Name, r.Category, r.Kind, r.Reduce, r.FromSupplements, r.Notes, now);
        ApplyAmount(line, r, Beginning, now);
        db.BudgetLines.Add(line);
        await db.SaveChangesAsync(ct);
        return BudgetLineDto.From(line, CurrentMonth, await SupplementsCostAsync(household, ct));
    }

    public Task<BudgetLineDto?> UpdateLineAsync(HouseholdContext household, Guid id, BudgetLineRequest r, CancellationToken ct) =>
        ChangeLineAsync(household, id, line =>
        {
            var now = clock.GetUtcNow();
            line.Update(r.Name, r.Category, r.Kind, r.Reduce, r.FromSupplements, r.Notes, now);
            ApplyAmount(line, r, CurrentMonth, now);
        }, ct);

    public Task<BudgetLineDto?> StopLineAsync(HouseholdContext household, Guid id, string? fromMonth, CancellationToken ct) =>
        ChangeLineAsync(household, id, line => line.Stop(MonthOr(fromMonth, CurrentMonth), clock.GetUtcNow()), ct);

    public Task<BudgetLineDto?> ResumeLineAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        ChangeLineAsync(household, id, line => line.Resume(clock.GetUtcNow()), ct);

    /// <summary>Deletes a line that no operation and no rule uses (otherwise 409: stop it instead).</summary>
    public async Task<bool> DeleteLineAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var line = await Lines(household).SingleOrDefaultAsync(l => l.Id == id, ct);
        if (line is null) return false;
        db.BudgetLines.Remove(line);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void ApplyAmount(BudgetLine line, BudgetLineRequest r, DateOnly defaultFrom, DateTimeOffset now)
    {
        if (!line.HasAmount || line.FromSupplements) return;
        if (r.Amount is not { } amount)
        {
            if (line.Amounts.Count == 0 && line.Kind != "income") throw new ArgumentException("Montant prévu obligatoire pour une ligne fixe ou une enveloppe.");
            return;
        }
        line.SetAmount(MonthOr(r.FromMonth, defaultFrom), amount, r.Period, r.Months, now);
    }

    private async Task<BudgetLineDto?> ChangeLineAsync(HouseholdContext household, Guid id, Action<BudgetLine> change, CancellationToken ct)
    {
        var line = await Lines(household).SingleOrDefaultAsync(l => l.Id == id, ct);
        if (line is null) return null;
        change(line);
        await db.SaveChangesAsync(ct);
        return BudgetLineDto.From(line, CurrentMonth, await SupplementsCostAsync(household, ct));
    }

    // Operations

    /// <param name="filter">all (operations of the month), unclassified, or spread (operations spread over several months that count in it).</param>
    public async Task<BudgetOperationListDto> ListOperationsAsync(HouseholdContext household, string? month, string? filter, string? query, CancellationToken ct)
    {
        var m = MonthOr(month, CurrentMonth);
        var f = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim();
        if (f is not ("all" or "unclassified" or "spread")) throw new ArgumentException("Filtre : all, unclassified ou spread.");
        var next = m.AddMonths(1);
        var source = f == "spread"
            ? Operations(household).Where(o => o.Spread > 1 && o.Date < next && o.Date >= m.AddMonths(-BudgetOperation.MaxSpread))
            : Operations(household).Where(o => o.Date >= m && o.Date < next);
        if (f == "unclassified") source = source.Where(o => o.LineId == null);
        var operations = await source.ToListAsync(ct);
        if (f == "spread") operations = operations.Where(o => o.CountsIn(m)).ToList();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = BudgetOperation.Normalize(query.Trim());
            operations = operations.Where(o => BudgetOperation.Normalize(o.Label).Contains(q, StringComparison.Ordinal)).ToList();
        }
        var lines = await LineMapAsync(household, ct);
        var unclassified = await Operations(household).CountAsync(o => o.LineId == null && o.Date >= m && o.Date < next, ct);
        var accounts = await db.BudgetAccounts.Where(a => a.HouseholdId == household.HouseholdId && a.OwnerUserId == household.UserId)
            .OrderBy(a => a.Account).ToListAsync(ct);
        return new BudgetOperationListDto(BudgetMonths.Format(m), unclassified, operations.Sum(o => o.Amount),
            operations.OrderByDescending(o => o.Date).ThenByDescending(o => o.CreatedAt).ThenBy(o => o.Label).Select(o => BudgetOperationDto.From(o, lines)).ToList(),
            accounts.Select(a => new BudgetAccountDto(a.Account, a.Balance, a.BalanceOn, a.LastImportAt, a.LastFrom, a.LastTo)).ToList());
    }

    public async Task<BudgetOperationDto?> GetOperationAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        await Operations(household).SingleOrDefaultAsync(o => o.Id == id, ct) is { } o ? BudgetOperationDto.From(o, await LineMapAsync(household, ct)) : null;

    public async Task<BudgetOperationDto> AddOperationAsync(HouseholdContext household, BudgetManualOperationRequest r, string by, CancellationToken ct)
    {
        var owner = await OwnerAsync(household, ct);
        var operation = BudgetOperation.Manual(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Date, r.Label, r.Amount, clock.GetUtcNow());
        if (r.LineId is { } lineId) operation.Classify(await LineOrThrowAsync(household, lineId, ct), r.Spread, by);
        db.BudgetOperations.Add(operation);
        await db.SaveChangesAsync(ct);
        return BudgetOperationDto.From(operation, await LineMapAsync(household, ct));
    }

    /// <summary>Deletes an operation typed by hand; imported operations stay (they would come back at the next import).</summary>
    public async Task<bool?> DeleteOperationAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var operation = await Operations(household).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (operation is null) return null;
        if (operation.Source != "manual") throw new ArgumentException("Une opération importée ne se supprime pas : rangez-la en hors budget si elle ne doit pas compter.");
        db.BudgetOperations.Remove(operation);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Classifies an operation (by hand or by the assistant), and optionally creates the matching rule.</summary>
    public async Task<(BudgetOperationDto Operation, BudgetRuleDto? Rule, int Classified)?> ClassifyAsync(HouseholdContext household, Guid id,
        BudgetClassifyRequest r, string by, CancellationToken ct)
    {
        var operation = await Operations(household).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (operation is null) return null;
        var line = r.LineId is { } lineId ? await LineOrThrowAsync(household, lineId, ct) : null;
        operation.Classify(line, r.Spread, by);
        BudgetRule? rule = null;
        var classified = 0;
        if (line is not null && !string.IsNullOrWhiteSpace(r.RuleText))
        {
            var position = await Rules(household).CountAsync(ct);
            rule = BudgetRule.Create(line, r.RuleText, operation.Amount < 0 ? "out" : "in", r.Spread, position, clock.GetUtcNow());
            db.BudgetRules.Add(rule);
            classified = await ApplyRuleAsync(household, rule, line, operation.Id, ct);
        }
        await db.SaveChangesAsync(ct);
        var lines = await LineMapAsync(household, ct);
        return (BudgetOperationDto.From(operation, lines), rule is null ? null : (await RuleDtosAsync(household, ct)).Single(x => x.Id == rule.Id), classified);
    }

    public async Task<BudgetImportDto> ImportAsync(HouseholdContext household, byte[] content, CancellationToken ct)
    {
        var statement = OfxStatement.Parse(content);
        var owner = await OwnerAsync(household, ct);
        var now = clock.GetUtcNow();
        var known = (await Operations(household).Where(o => o.Account == statement.Account && o.BankId != null).Select(o => o.BankId!).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var rules = await Rules(household).ToListAsync(ct);
        var lines = await Lines(household).ToDictionaryAsync(l => l.Id, ct);
        var (created, duplicates) = BudgetReports.PlanImport(statement, known, rules, lines, owner.HouseholdId, owner.UserId, owner.ProfileId, now);
        db.BudgetOperations.AddRange(created);
        var account = await db.BudgetAccounts.SingleOrDefaultAsync(a => a.OwnerUserId == owner.UserId && a.HouseholdId == owner.HouseholdId
            && a.Account == statement.Account, ct);
        if (account is null)
        {
            account = BudgetAccount.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, statement.Account);
            db.BudgetAccounts.Add(account);
        }
        account.Imported(statement, now);
        await db.SaveChangesAsync(ct);
        var classified = created.Count(o => o.LineId is not null);
        return new BudgetImportDto(statement.Account, statement.From, statement.To, statement.Transactions.Count, duplicates, created.Count, classified,
            created.Count - classified, account.Balance, account.BalanceOn,
            created.OrderByDescending(o => o.Date).Select(o => BudgetOperationDto.From(o, lines)).ToList());
    }

    // Rules

    public async Task<IReadOnlyList<BudgetRuleDto>> ListRulesAsync(HouseholdContext household, CancellationToken ct) => await RuleDtosAsync(household, ct);

    public async Task<BudgetRuleResultDto> CreateRuleAsync(HouseholdContext household, BudgetRuleRequest r, CancellationToken ct)
    {
        var line = await LineOrThrowAsync(household, r.LineId, ct);
        var rules = await Rules(household).OrderBy(x => x.Position).ThenBy(x => x.CreatedAt).ToListAsync(ct);
        var rule = BudgetRule.Create(line, r.Text, r.Direction, r.Spread, rules.Count, clock.GetUtcNow());
        db.BudgetRules.Add(rule);
        Reorder(rules, rule, r.Position ?? rules.Count);
        var classified = await ApplyRuleAsync(household, rule, line, null, ct);
        await db.SaveChangesAsync(ct);
        return new BudgetRuleResultDto((await RuleDtosAsync(household, ct)).Single(x => x.Id == rule.Id), classified);
    }

    public async Task<BudgetRuleResultDto?> UpdateRuleAsync(HouseholdContext household, Guid id, BudgetRuleRequest r, CancellationToken ct)
    {
        var rules = await Rules(household).OrderBy(x => x.Position).ThenBy(x => x.CreatedAt).ToListAsync(ct);
        var rule = rules.SingleOrDefault(x => x.Id == id);
        if (rule is null) return null;
        var line = await LineOrThrowAsync(household, r.LineId, ct);
        rule.Update(line, r.Text, r.Direction, r.Spread);
        if (r.Position is { } position) Reorder(rules.Where(x => x.Id != id).ToList(), rule, position);
        var classified = await ApplyRuleAsync(household, rule, line, null, ct);
        await db.SaveChangesAsync(ct);
        return new BudgetRuleResultDto((await RuleDtosAsync(household, ct)).Single(x => x.Id == rule.Id), classified);
    }

    public async Task<IReadOnlyList<BudgetRuleDto>?> MoveRuleAsync(HouseholdContext household, Guid id, int position, CancellationToken ct)
    {
        var rules = await Rules(household).OrderBy(x => x.Position).ThenBy(x => x.CreatedAt).ToListAsync(ct);
        var rule = rules.SingleOrDefault(x => x.Id == id);
        if (rule is null) return null;
        Reorder(rules.Where(x => x.Id != id).ToList(), rule, position);
        await db.SaveChangesAsync(ct);
        return await RuleDtosAsync(household, ct);
    }

    /// <summary>Deletes a rule; the operations it classified stay in their line.</summary>
    public async Task<bool> DeleteRuleAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var rules = await Rules(household).OrderBy(x => x.Position).ThenBy(x => x.CreatedAt).ToListAsync(ct);
        var rule = rules.SingleOrDefault(x => x.Id == id);
        if (rule is null) return false;
        db.BudgetRules.Remove(rule);
        var position = 0;
        foreach (var other in rules.Where(x => x.Id != id)) other.MoveTo(position++);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Reorder(List<BudgetRule> others, BudgetRule rule, int position)
    {
        var index = Math.Clamp(position, 0, others.Count);
        others.Insert(index, rule);
        for (var i = 0; i < others.Count; i++) others[i].MoveTo(i);
    }

    /// <summary>A new or changed rule classifies the unclassified operations it matches (classified ones never change).</summary>
    private async Task<int> ApplyRuleAsync(HouseholdContext household, BudgetRule rule, BudgetLine line, Guid? except, CancellationToken ct)
    {
        var pending = await Operations(household).Where(o => o.LineId == null && o.Id != except).ToListAsync(ct);
        var count = 0;
        foreach (var operation in pending.Where(o => rule.Matches(o.Label, o.Amount) && !(line.Kind == "income" && o.Amount < 0)))
        {
            operation.Classify(line, rule.Spread, "rule", rule.Id);
            count++;
        }
        return count;
    }

    private async Task<IReadOnlyList<BudgetRuleDto>> RuleDtosAsync(HouseholdContext household, CancellationToken ct)
    {
        var rules = await Rules(household).OrderBy(r => r.Position).ThenBy(r => r.CreatedAt).ToListAsync(ct);
        var lines = await LineMapAsync(household, ct);
        var operations = await Operations(household).Select(o => new { o.Label, o.Amount }).ToListAsync(ct);
        var firsts = operations.Select(o => BudgetRule.FirstMatch(rules, o.Label, o.Amount)?.Id).Where(x => x is not null).GroupBy(x => x!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        return rules.Select(r => new BudgetRuleDto(r.Id, r.Position, r.Text, r.Direction, r.LineId, lines.TryGetValue(r.LineId, out var l) ? l.Name : "",
            r.Spread, firsts.GetValueOrDefault(r.Id))).ToList();
    }

    // Reports

    public async Task<BudgetMonthDto> MonthAsync(HouseholdContext household, string? month, CancellationToken ct)
    {
        var m = MonthOr(month, CurrentMonth);
        var lines = await Lines(household).ToListAsync(ct);
        var operations = await Operations(household).Where(o => o.Date < m.AddMonths(1) && o.Date >= m.AddMonths(-BudgetOperation.MaxSpread)).ToListAsync(ct);
        return BudgetReports.Month(lines, operations, m, Today, await SupplementsCostAsync(household, ct));
    }

    public async Task<BudgetTrendDto> TrendAsync(HouseholdContext household, int months, string? until, CancellationToken ct)
    {
        if (months is < 2 or > 36) throw new ArgumentException("Évolution : de 2 à 36 mois.");
        var last = MonthOr(until, CurrentMonth);
        var first = last.AddMonths(-months + 1);
        var lines = await Lines(household).ToListAsync(ct);
        var operations = await Operations(household).Where(o => o.Date < last.AddMonths(1) && o.Date >= first.AddMonths(-BudgetOperation.MaxSpread)).ToListAsync(ct);
        return BudgetReports.Trend(lines, operations, last, months, await SupplementsCostAsync(household, ct));
    }

    private async Task<BudgetLine> LineOrThrowAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        await Lines(household).SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw new ArgumentException("Ligne introuvable dans votre budget.");

    private async Task<IReadOnlyDictionary<Guid, BudgetLine>> LineMapAsync(HouseholdContext household, CancellationToken ct) =>
        await Lines(household).ToDictionaryAsync(l => l.Id, ct);
}
