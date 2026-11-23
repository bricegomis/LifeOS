using LifeOS.Domain.Budget;

namespace LifeOS.Application.Budget;

public sealed record BudgetAmountDto(string FromMonth, decimal Amount, string Period, int Months);

/// <param name="Kind">fixed, envelope, extra, off or income.</param>
/// <param name="Planned">Monthly planned amount of the current month (spread when per year or every N months).</param>
public sealed record BudgetLineDto(Guid Id, string Name, string Category, string Kind, bool Reduce, bool FromSupplements, string? Notes,
    string? StoppedFrom, bool Active, BudgetAmountDto? Amount, decimal Planned, IReadOnlyList<BudgetAmountDto> History)
{
    public static BudgetLineDto From(BudgetLine l, DateOnly month, decimal supplementsMonthlyCost) => new(
        l.Id, l.Name, l.Category, l.Kind, l.Reduce, l.FromSupplements, l.Notes, l.StoppedFrom is { } s ? BudgetMonths.Format(s) : null, l.ActiveIn(month),
        l.AmountFor(month) is { } a ? ToDto(a) : l.Amounts.OrderBy(x => x.FromMonth).Select(ToDto).FirstOrDefault(),
        l.PlannedFor(month, supplementsMonthlyCost), l.Amounts.OrderBy(a => a.FromMonth).Select(ToDto).ToList());

    private static BudgetAmountDto ToDto(BudgetAmount a) => new(BudgetMonths.Format(a.FromMonth), a.Amount, a.Period, a.Months);
}

/// <param name="SupplementsMonthlyCost">Monthly cost of the account's active supplements, computed by Health.</param>
public sealed record BudgetLineListDto(string Month, decimal SupplementsMonthlyCost, IReadOnlyList<string> Categories, IReadOnlyList<BudgetLineDto> Lines);

/// <param name="Source">ofx or manual.</param>
/// <param name="ClassifiedBy">rule, hand or assistant; null while unclassified.</param>
public sealed record BudgetOperationDto(Guid Id, DateOnly Date, DateOnly? PostedOn, string Label, decimal Amount, string Source, string? Account,
    Guid? LineId, string? LineName, int Spread, string? ClassifiedBy, Guid? RuleId)
{
    public static BudgetOperationDto From(BudgetOperation o, IReadOnlyDictionary<Guid, BudgetLine> lines) => new(
        o.Id, o.Date, o.PostedOn, o.Label, o.Amount, o.Source, o.Account, o.LineId, o.LineId is { } id && lines.TryGetValue(id, out var l) ? l.Name : null,
        o.Spread, o.ClassifiedBy, o.RuleId);
}

public sealed record BudgetAccountDto(string Account, decimal? Balance, DateOnly? BalanceOn, DateTimeOffset? LastImportAt, DateOnly? LastFrom, DateOnly? LastTo);

/// <param name="Total">Sum of the listed operations.</param>
public sealed record BudgetOperationListDto(string Month, int UnclassifiedCount, decimal Total, IReadOnlyList<BudgetOperationDto> Operations,
    IReadOnlyList<BudgetAccountDto> Accounts);

/// <param name="Read">Transactions in the file.</param>
/// <param name="Duplicates">Already imported (same account and bank id), ignored.</param>
/// <param name="Classified">New operations put in a line by a rule.</param>
public sealed record BudgetImportDto(string Account, DateOnly? From, DateOnly? To, int Read, int Duplicates, int Created, int Classified,
    int Unclassified, decimal? Balance, DateOnly? BalanceOn, IReadOnlyList<BudgetOperationDto> Operations);

/// <param name="Direction">any, out (expenses) or in (incomes).</param>
/// <param name="Matches">Operations already imported that this rule would classify first.</param>
public sealed record BudgetRuleDto(Guid Id, int Position, string Text, string Direction, Guid LineId, string LineName, int Spread, int Matches);

/// <summary>A rule saved, and the unclassified operations it classified right away.</summary>
public sealed record BudgetRuleResultDto(BudgetRuleDto Rule, int Classified);

/// <summary>A line in a month: planned, real (spread shares included; spent for an expense, received for an income) and gap.</summary>
public sealed record BudgetMonthLineDto(BudgetLineDto Line, decimal Planned, decimal Real, decimal Gap, bool Over, int Operations);

public sealed record BudgetMonthCategoryDto(string Category, decimal Planned, decimal Real, IReadOnlyList<BudgetMonthLineDto> Lines);

/// <param name="Status">past, current or future.</param>
/// <param name="Spent">Real expenses, unclassified expenses included.</param>
/// <param name="Rest">Received − spent; <paramref name="PlannedRest"/> planned incomes − planned expenses.</param>
/// <param name="Off">Off-budget movements, which count nowhere.</param>
public sealed record BudgetMonthDto(string Month, string Status, decimal PlannedIncome, decimal Received, decimal PlannedExpenses, decimal Spent,
    decimal PlannedRest, decimal Rest, decimal Extras, int UnclassifiedCount, decimal UnclassifiedTotal, decimal Off, decimal SupplementsMonthlyCost,
    IReadOnlyList<BudgetMonthLineDto> Envelopes, IReadOnlyList<BudgetMonthCategoryDto> Categories, IReadOnlyList<BudgetMonthLineDto> Incomes,
    IReadOnlyList<BudgetMonthLineDto> OffLines);

public sealed record BudgetTrendMonthDto(string Month, decimal Spent, decimal Planned, decimal Extras, decimal Received,
    IReadOnlyDictionary<string, decimal> ByCategory);

public sealed record BudgetTrendLineDto(Guid LineId, string Name, IReadOnlyList<decimal> Values);

/// <param name="Reduce">Lines marked "à diminuer", their real per month.</param>
public sealed record BudgetTrendDto(IReadOnlyList<BudgetTrendMonthDto> Months, IReadOnlyList<BudgetTrendLineDto> Reduce);

/// <summary>A line's page: the line, its real and planned month by month, its rules.</summary>
public sealed record BudgetLineDetailDto(BudgetLineDto Line, IReadOnlyList<BudgetLineMonthDto> Months, IReadOnlyList<BudgetRuleDto> Rules);

public sealed record BudgetLineMonthDto(string Month, decimal Planned, decimal Real);
