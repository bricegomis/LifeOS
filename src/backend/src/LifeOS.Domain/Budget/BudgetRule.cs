using LifeOS.Domain.Common;

namespace LifeOS.Domain.Budget;

/// <summary>
/// A classification rule (ADR 0020): "label contains …" (ignoring case and accents), optionally for expenses or
/// incomes only, puts an imported operation in a line with an optional spread. Rules apply in their order; the
/// first that matches wins. Written by the owner or by the assistant on request; the API guesses nothing else.
/// </summary>
public sealed class BudgetRule : Entity
{
    public const int MaxTextLength = 100;
    public static readonly IReadOnlyList<string> Directions = ["any", "out", "in"];

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Text { get; private set; } = "";
    public string Direction { get; private set; } = "any";
    public Guid LineId { get; private set; }
    public int Spread { get; private set; } = 1;
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private BudgetRule() { }

    public static BudgetRule Create(BudgetLine line, string text, string? direction, int spread, int position, DateTimeOffset now)
    {
        var rule = new BudgetRule
        {
            Id = Guid.NewGuid(), HouseholdId = line.HouseholdId, OwnerUserId = line.OwnerUserId, MemberProfileId = line.MemberProfileId,
            Position = position, CreatedAt = now,
        };
        rule.Update(line, text, direction, spread);
        return rule;
    }

    public void Update(BudgetLine line, string text, string? direction, int spread)
    {
        if (line.OwnerUserId != OwnerUserId || line.HouseholdId != HouseholdId) throw new ArgumentException("Ligne introuvable.");
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxTextLength) throw new ArgumentException($"Texte à chercher obligatoire ({MaxTextLength} caractères maximum).");
        var dir = string.IsNullOrWhiteSpace(direction) ? "any" : direction.Trim();
        if (!Directions.Contains(dir)) throw new ArgumentException("Sens : any, out (dépenses) ou in (revenus).");
        if (spread is < 1 or > BudgetOperation.MaxSpread) throw new ArgumentException($"Étalement : de 1 à {BudgetOperation.MaxSpread} mois.");
        if (line.Kind == "income" && dir == "out") throw new ArgumentException("Une règle de dépenses ne range pas dans un revenu.");
        Text = text.Trim();
        Direction = dir;
        LineId = line.Id;
        Spread = spread;
    }

    public void MoveTo(int position) => Position = position;

    public bool Matches(string label, decimal amount)
    {
        if (Direction == "out" && amount > 0) return false;
        if (Direction == "in" && amount < 0) return false;
        return BudgetOperation.Normalize(label).Contains(BudgetOperation.Normalize(Text), StringComparison.Ordinal);
    }

    /// <summary>The first rule, in order, that matches.</summary>
    public static BudgetRule? FirstMatch(IEnumerable<BudgetRule> rules, string label, decimal amount) =>
        rules.OrderBy(r => r.Position).ThenBy(r => r.CreatedAt).FirstOrDefault(r => r.Matches(label, amount));
}
