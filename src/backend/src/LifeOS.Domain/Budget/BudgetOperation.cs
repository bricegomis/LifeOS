using System.Globalization;
using System.Text;
using LifeOS.Domain.Common;

namespace LifeOS.Domain.Budget;

/// <summary>
/// A movement of the account (ADR 0020): imported from a bank statement (identified by the bank's id, never
/// created twice) or typed by hand. It counts in its line, spread over <see cref="Spread"/> months from its month.
/// </summary>
public sealed class BudgetOperation : Entity
{
    public const int MaxLabelLength = 300;
    public const int MaxAccountLength = 64;
    public const int MaxBankIdLength = 255;
    public const int MaxSpread = 36;

    public static readonly IReadOnlyList<string> Sources = ["ofx", "manual"];
    public static readonly IReadOnlyList<string> ClassifiedBys = ["rule", "hand", "assistant"];

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Source { get; private set; } = "manual";
    public string? Account { get; private set; }
    public string? BankId { get; private set; }
    /// <summary>Day of the purchase (read from a card label) or of the debit.</summary>
    public DateOnly Date { get; private set; }
    public DateOnly? PostedOn { get; private set; }
    public string Label { get; private set; } = "";
    /// <summary>Negative for an expense, positive for an income or a refund.</summary>
    public decimal Amount { get; private set; }
    public Guid? LineId { get; private set; }
    public int Spread { get; private set; } = 1;
    public string? ClassifiedBy { get; private set; }
    public Guid? RuleId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private BudgetOperation() { }

    public static BudgetOperation Imported(Guid householdId, Guid ownerUserId, Guid memberProfileId, string account, StatementTransaction t, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(t.BankId) || t.BankId.Length > MaxBankIdLength) throw new ArgumentException("Identifiant bancaire de l'opération manquant.");
        var operation = Create(householdId, ownerUserId, memberProfileId, t.Date, t.Label, t.Amount, now);
        operation.Source = "ofx";
        operation.Account = Clean(account, MaxAccountLength, "Compte");
        operation.BankId = t.BankId.Trim();
        operation.PostedOn = t.PostedOn;
        return operation;
    }

    public static BudgetOperation Manual(Guid householdId, Guid ownerUserId, Guid memberProfileId, DateOnly date, string label, decimal amount, DateTimeOffset now) =>
        Create(householdId, ownerUserId, memberProfileId, date, label, amount, now);

    private static BudgetOperation Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, DateOnly date, string label, decimal amount, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        if (amount == 0 || Math.Abs(amount) > 10_000_000) throw new ArgumentException("Montant invalide (différent de 0).");
        return new BudgetOperation
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId, Date = date,
            Label = Clean(label, MaxLabelLength, "Libellé"), Amount = Math.Round(amount, 2), CreatedAt = now,
        };
    }

    private static string Clean(string? text, int max, string what)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException($"{what} obligatoire.");
        var value = text.Trim();
        return value.Length > max ? value[..max] : value;
    }

    public void EditManual(DateOnly date, string label, decimal amount)
    {
        if (Source != "manual") throw new ArgumentException("Seule une opération saisie à la main se modifie.");
        if (amount == 0 || Math.Abs(amount) > 10_000_000) throw new ArgumentException("Montant invalide (différent de 0).");
        Date = date;
        Label = Clean(label, MaxLabelLength, "Libellé");
        Amount = Math.Round(amount, 2);
    }

    /// <summary>Puts the operation in a line (or back to unclassified with null), spread over 1 to 36 months.</summary>
    public void Classify(BudgetLine? line, int spread, string by, Guid? ruleId = null)
    {
        if (spread is < 1 or > MaxSpread) throw new ArgumentException($"Étalement : de 1 à {MaxSpread} mois.");
        if (line is not null)
        {
            if (line.OwnerUserId != OwnerUserId || line.HouseholdId != HouseholdId) throw new ArgumentException("Ligne introuvable.");
            if (line.Kind == "income" && Amount < 0) throw new ArgumentException("Une dépense ne se range pas dans un revenu.");
        }
        if (!ClassifiedBys.Contains(by)) throw new ArgumentException("Rangement : rule, hand ou assistant.");
        LineId = line?.Id;
        Spread = line is null ? 1 : spread;
        ClassifiedBy = line is null ? null : by;
        RuleId = line is null ? null : ruleId;
    }

    public DateOnly Month => BudgetMonths.Of(Date);

    /// <summary>True when a share of the operation counts in <paramref name="month"/>.</summary>
    public bool CountsIn(DateOnly month)
    {
        var offset = BudgetMonths.Between(Month, BudgetMonths.Of(month));
        return offset >= 0 && offset < Spread;
    }

    /// <summary>The share of the amount that counts in each month it covers.</summary>
    public decimal Share => Math.Round(Amount / Spread, 2);

    /// <summary>Label upper-cased without accents, as rules compare it.</summary>
    public static string Normalize(string text)
    {
        var d = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
