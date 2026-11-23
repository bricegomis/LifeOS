using System.Text;
using LifeOS.Application.Budget;
using LifeOS.Domain.Budget;

namespace LifeOS.Application.Tests.Budget;

public sealed class BudgetReportsTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October = new(2026, 10, 1);
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static BudgetLine Line(string name, string kind, string? category, decimal? amount = null, bool supplements = false)
    {
        var line = BudgetLine.Create(Household, Owner, Profile, name, category, kind, false, supplements, null, Now);
        if (amount is { } a) line.SetAmount(new DateOnly(2000, 1, 1), a, null, null, Now);
        return line;
    }

    private static BudgetOperation Op(DateOnly date, string label, decimal amount, BudgetLine? line = null, int spread = 1)
    {
        var o = BudgetOperation.Manual(Household, Owner, Profile, date, label, amount, Now);
        if (line is not null) o.Classify(line, spread, "hand");
        return o;
    }

    [Fact]
    public void A_month_compares_planned_and_real_with_spread_shares_unclassified_and_off_budget()
    {
        var food = Line("Courses", "envelope", "Alimentation", 800);
        var works = Line("Travaux", "envelope", "Travaux", 100);
        var loan = Line("Prêt", "fixed", "Prêts", 508.8m);
        var supplements = Line("Compléments", "envelope", "Santé", supplements: true);
        var gifts = Line("Cadeaux", "extra", "Extras");
        var salary = Line("Salaire", "income", null, 2930);
        var transfer = Line("Épargne", "off", null);
        var ops = new List<BudgetOperation>
        {
            Op(October.AddDays(4), "LECLERC", -350, food), Op(October.AddDays(20), "AVOIR LECLERC", 10, food),
            Op(October.AddDays(6), "LEROY MERLIN", -130, works), Op(October.AddDays(4), "CA CONSUMER", -508.8m, loan),
            Op(new DateOnly(2026, 9, 12), "NUTRIMUSCLE", -412.2m, supplements, 6), Op(October.AddDays(20), "PAYPAL", -112, gifts),
            Op(October.AddDays(29), "VIR SEPA EMPLOYEUR", 2929.36m, salary), Op(October.AddDays(11), "VIR SEPA MOI", -500, transfer),
            Op(October.AddDays(15), "SWILE", -25), Op(new DateOnly(2026, 11, 2), "LECLERC", -60, food),
        };
        var month = BudgetReports.Month([food, works, loan, supplements, gifts, salary, transfer], ops, October, Today, 70.5m);

        Assert.Equal("past", month.Status);
        Assert.Equal((2930m, 2929.36m), (month.PlannedIncome, month.Received));
        Assert.Equal(800 + 100 + 508.8m + 70.5m, month.PlannedExpenses);
        Assert.Equal(340 + 130 + 508.8m + 68.7m + 112 + 25, month.Spent);
        Assert.Equal((112m, 1, -25m, -500m), (month.Extras, month.UnclassifiedCount, month.UnclassifiedTotal, month.Off));
        Assert.Equal(month.Received - month.Spent, month.Rest);
        var envelopes = month.Envelopes.ToDictionary(e => e.Line.Name);
        Assert.True(envelopes["Travaux"].Over);
        Assert.Equal((340m, false), (envelopes["Courses"].Real, envelopes["Courses"].Over));
        Assert.Equal("Extras", month.Categories[^1].Category);
    }

    [Fact]
    public void An_import_skips_known_bank_ids_and_classifies_with_the_first_matching_rule()
    {
        var food = Line("Courses", "envelope", "Alimentation", 800);
        var salary = Line("Salaire", "income", null, 2930);
        var rules = new[] { BudgetRule.Create(food, "LECLERC", "out", 1, 0, Now), BudgetRule.Create(salary, "EMPLOYEUR", "any", 1, 1, Now) };
        var ofx = "<OFX><ACCTID>42</ACCTID><BANKTRANLIST>" +
            "<STMTTRN><DTPOSTED>20261003</DTPOSTED><TRNAMT>-45.23</TRNAMT><FITID>a</FITID><NAME>CARTE 02/10/26 LECLERC REZE CB*1</NAME></STMTTRN>" +
            "<STMTTRN><DTPOSTED>20261030</DTPOSTED><TRNAMT>2929.36</TRNAMT><FITID>b</FITID><NAME>VIR SEPA EMPLOYEUR</NAME></STMTTRN>" +
            "<STMTTRN><DTPOSTED>20261030</DTPOSTED><TRNAMT>-2</TRNAMT><FITID>c</FITID><NAME>FRAIS EMPLOYEUR</NAME></STMTTRN>" +
            "<STMTTRN><DTPOSTED>20261030</DTPOSTED><TRNAMT>-5</TRNAMT><FITID>d</FITID><NAME>DEJA LA</NAME></STMTTRN></BANKTRANLIST></OFX>";
        var (created, duplicates) = BudgetReports.PlanImport(OfxStatement.Parse(Encoding.UTF8.GetBytes(ofx)), new HashSet<string> { "d" }, rules,
            new Dictionary<Guid, BudgetLine> { [food.Id] = food, [salary.Id] = salary }, Household, Owner, Profile, Now);
        Assert.Equal((3, 1), (created.Count, duplicates));
        Assert.Equal((food.Id, new DateOnly(2026, 10, 2), "rule"), (created[0].LineId!.Value, created[0].Date, created[0].ClassifiedBy!));
        Assert.Equal(salary.Id, created[1].LineId);
        // An expense never lands in an income line, even through an "any" rule.
        Assert.Null(created[2].LineId);
    }
}
