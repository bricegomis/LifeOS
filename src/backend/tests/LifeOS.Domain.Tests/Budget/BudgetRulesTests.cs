using LifeOS.Domain.Budget;

namespace LifeOS.Domain.Tests.Budget;

public sealed class BudgetRulesTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly November = new(2026, 11, 1);

    private static BudgetLine Line(string kind, string? category = "Alimentation") =>
        BudgetLine.Create(Household, Owner, Profile, "Courses", category, kind, false, false, null, Now);

    [Fact]
    public void A_planned_amount_changes_from_a_month_and_earlier_months_keep_theirs()
    {
        var line = Line("envelope");
        line.SetAmount(new DateOnly(2000, 1, 1), 800, "month", null, Now);
        line.SetAmount(November, 600, null, null, Now);
        Assert.Equal(800, line.PlannedFor(new DateOnly(2026, 10, 15), 0));
        Assert.Equal(600, line.PlannedFor(November, 0));
        line.SetAmount(November, 300, "year", null, Now);
        Assert.Equal((25m, 2), (line.PlannedFor(new DateOnly(2027, 3, 1), 0), line.Amounts.Count));
        line.SetAmount(November, 9, "months", 3, Now);
        Assert.Equal(3, line.PlannedFor(November, 0));
        line.Stop(November, Now);
        Assert.Equal((0m, 800m), (line.PlannedFor(November, 0), line.PlannedFor(new DateOnly(2026, 10, 1), 0)));
    }

    [Fact]
    public void Kinds_have_their_own_rules()
    {
        Assert.Equal(("Revenus", "Hors budget"), (Line("income", "Logement").Category, Line("off", null).Category));
        Assert.Throws<ArgumentException>(() => Line("extra").SetAmount(November, 10, null, null, Now));
        Assert.Throws<ArgumentException>(() => Line("income").SetAmount(November, 10, "year", null, Now));
        Assert.Throws<ArgumentException>(() => Line("fixed", "Revenus"));
        Assert.Throws<ArgumentException>(() => BudgetLine.Create(Household, Owner, Profile, "X", "Santé", "extra", false, true, null, Now));
        var supplements = BudgetLine.Create(Household, Owner, Profile, "Compléments", "Santé", "envelope", true, true, null, Now);
        Assert.Equal(70.5m, supplements.PlannedFor(November, 70.5m));
    }

    [Fact]
    public void A_spread_operation_counts_a_share_in_each_month_it_covers()
    {
        var operation = BudgetOperation.Manual(Household, Owner, Profile, new DateOnly(2026, 9, 12), "NUTRIMUSCLE", -412.2m, Now);
        operation.Classify(Line("envelope"), 6, "hand");
        Assert.Equal(-68.7m, operation.Share);
        Assert.True(operation.CountsIn(new DateOnly(2026, 9, 1)) && operation.CountsIn(new DateOnly(2027, 2, 1)));
        Assert.False(operation.CountsIn(new DateOnly(2027, 3, 1)) || operation.CountsIn(new DateOnly(2026, 8, 1)));
        Assert.Throws<ArgumentException>(() => operation.Classify(Line("envelope"), 37, "hand"));
        Assert.Throws<ArgumentException>(() => operation.Classify(Line("income"), 1, "hand"));
        operation.Classify(null, 6, "hand");
        Assert.Equal((null, 1, null), (operation.LineId, operation.Spread, operation.ClassifiedBy));
    }

    [Fact]
    public void Rules_compare_without_case_or_accents_and_the_first_in_order_wins()
    {
        var salary = Line("income");
        var other = BudgetLine.Create(Household, Owner, Profile, "Autres revenus", null, "income", false, false, null, Now);
        var cse = BudgetRule.Create(other, "cse younup", "in", 1, 0, Now);
        var pay = BudgetRule.Create(salary, "YOUNUP", "in", 1, 1, Now);
        Assert.Same(cse, BudgetRule.FirstMatch([pay, cse], "VIR INST CSE YOUNUP", 100));
        Assert.Same(pay, BudgetRule.FirstMatch([pay, cse], "VIR SEPA YOUNUP", 2929.36m));
        Assert.Null(BudgetRule.FirstMatch([pay, cse], "VIR SEPA YOUNUP", -10));
        var food = BudgetRule.Create(Line("envelope"), "Épicerie", null, 1, 2, Now);
        Assert.True(food.Matches("CARTE 03/10/26 EPICERIE DU COIN CB*1", -3));
        Assert.Throws<ArgumentException>(() => BudgetRule.Create(salary, "X", "out", 1, 0, Now));
    }
}
