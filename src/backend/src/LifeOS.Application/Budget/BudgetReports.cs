using LifeOS.Domain.Budget;

namespace LifeOS.Application.Budget;

/// <summary>Budget months, trend and line history, computed on read from lines and operations (ADR 0020).</summary>
public static class BudgetReports
{
    /// <summary>Real of a line in a month: spent for an expense, received for an income, signed for an off-budget line.</summary>
    public static decimal Real(BudgetLine line, IEnumerable<BudgetOperation> operations, DateOnly month)
    {
        var sum = operations.Where(o => o.LineId == line.Id && o.CountsIn(month)).Sum(o => o.Share);
        return line.IsExpense ? -sum : sum;
    }

    public static BudgetMonthDto Month(IReadOnlyList<BudgetLine> lines, IReadOnlyList<BudgetOperation> operations, DateOnly month, DateOnly today,
        decimal supplementsCost)
    {
        month = BudgetMonths.Of(month);
        var current = BudgetMonths.Of(today);
        var status = month < current ? "past" : month == current ? "current" : "future";
        BudgetMonthLineDto Row(BudgetLine l)
        {
            var planned = l.PlannedFor(month, supplementsCost);
            var real = Real(l, operations, month);
            var count = operations.Count(o => o.LineId == l.Id && o.CountsIn(month));
            return new BudgetMonthLineDto(BudgetLineDto.From(l, month, supplementsCost), planned, real, real - planned,
                l.Kind is "fixed" or "envelope" && real > planned + 0.005m, count);
        }
        bool Shown(BudgetLine l) => l.ActiveIn(month) || operations.Any(o => o.LineId == l.Id && o.CountsIn(month));
        var expenses = lines.Where(l => l.IsExpense && Shown(l)).Select(Row).ToList();
        var incomes = lines.Where(l => l.Kind == "income" && Shown(l)).OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase).Select(Row).ToList();
        var off = lines.Where(l => l.Kind == "off" && Shown(l)).OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase).Select(Row).ToList();
        var unclassified = operations.Where(o => o.LineId is null && o.Month == month).ToList();
        var unclassifiedSpent = -unclassified.Where(o => o.Amount < 0).Sum(o => o.Amount);
        var unclassifiedIn = unclassified.Where(o => o.Amount > 0).Sum(o => o.Amount);
        var categories = expenses.GroupBy(r => r.Line.Category)
            .Select(g => new BudgetMonthCategoryDto(g.Key, g.Sum(r => r.Planned), g.Sum(r => r.Real),
                g.OrderBy(r => r.Line.Name, StringComparer.CurrentCultureIgnoreCase).ToList()))
            .OrderBy(c => c.Category == "Extras").ThenBy(c => c.Category, StringComparer.CurrentCultureIgnoreCase).ToList();
        var plannedIncome = incomes.Sum(r => r.Planned);
        var received = incomes.Sum(r => r.Real) + unclassifiedIn;
        var plannedExpenses = expenses.Sum(r => r.Planned);
        var spent = expenses.Sum(r => r.Real) + unclassifiedSpent;
        return new BudgetMonthDto(BudgetMonths.Format(month), status, plannedIncome, received, plannedExpenses, spent, plannedIncome - plannedExpenses,
            received - spent, expenses.Where(r => r.Line.Kind == "extra").Sum(r => r.Real), unclassified.Count, unclassified.Sum(o => o.Amount),
            off.Sum(r => r.Real), supplementsCost,
            expenses.Where(r => r.Line.Kind == "envelope" && r.Line.Active).OrderBy(r => r.Line.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            categories, incomes, off);
    }

    public static BudgetTrendDto Trend(IReadOnlyList<BudgetLine> lines, IReadOnlyList<BudgetOperation> operations, DateOnly lastMonth, int months,
        decimal supplementsCost)
    {
        var list = Enumerable.Range(0, months).Select(i => BudgetMonths.Of(lastMonth).AddMonths(i - months + 1)).ToList();
        var expenses = lines.Where(l => l.IsExpense).ToList();
        var trend = list.Select(m =>
        {
            var byCategory = expenses.GroupBy(l => l.Category).ToDictionary(g => g.Key, g => g.Sum(l => Real(l, operations, m)));
            var unclassified = -operations.Where(o => o.LineId is null && o.Month == m && o.Amount < 0).Sum(o => o.Amount);
            if (unclassified != 0) byCategory["Non classé"] = unclassified;
            return new BudgetTrendMonthDto(BudgetMonths.Format(m), byCategory.Values.Sum(),
                expenses.Sum(l => l.PlannedFor(m, supplementsCost)), expenses.Where(l => l.Kind == "extra").Sum(l => Real(l, operations, m)),
                lines.Where(l => l.Kind == "income").Sum(l => Real(l, operations, m)) + operations.Where(o => o.LineId is null && o.Month == m && o.Amount > 0).Sum(o => o.Amount),
                byCategory.Where(kv => kv.Value != 0).ToDictionary(kv => kv.Key, kv => kv.Value));
        }).ToList();
        var reduce = expenses.Where(l => l.Reduce && l.StoppedFrom is null).OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(l => new BudgetTrendLineDto(l.Id, l.Name, list.Select(m => Real(l, operations, m)).ToList())).ToList();
        return new BudgetTrendDto(trend, reduce);
    }

    public static IReadOnlyList<BudgetLineMonthDto> LineMonths(BudgetLine line, IReadOnlyList<BudgetOperation> operations, DateOnly lastMonth, int months,
        decimal supplementsCost) =>
        Enumerable.Range(0, months).Select(i => BudgetMonths.Of(lastMonth).AddMonths(i - months + 1))
            .Select(m => new BudgetLineMonthDto(BudgetMonths.Format(m), line.PlannedFor(m, supplementsCost), Real(line, operations, m))).ToList();

    /// <summary>
    /// Plans an import (ADR 0020): transactions already imported for the account (same bank id) are skipped, the new
    /// ones are created and classified by the first matching rule. Pure; the caller saves the result.
    /// </summary>
    public static (IReadOnlyList<BudgetOperation> Created, int Duplicates) PlanImport(OfxStatement statement, ISet<string> knownBankIds,
        IReadOnlyList<BudgetRule> rules, IReadOnlyDictionary<Guid, BudgetLine> lines, Guid householdId, Guid ownerUserId, Guid profileId, DateTimeOffset now)
    {
        var created = new List<BudgetOperation>();
        var seen = new HashSet<string>(knownBankIds);
        var duplicates = 0;
        foreach (var t in statement.Transactions)
        {
            if (!seen.Add(t.BankId)) { duplicates++; continue; }
            var operation = BudgetOperation.Imported(householdId, ownerUserId, profileId, statement.Account, t, now);
            if (BudgetRule.FirstMatch(rules, operation.Label, operation.Amount) is { } rule && lines.TryGetValue(rule.LineId, out var line)
                && !(line.Kind == "income" && operation.Amount < 0))
                operation.Classify(line, rule.Spread, "rule", rule.Id);
            created.Add(operation);
        }
        return (created, duplicates);
    }
}
