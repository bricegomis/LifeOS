using System.Globalization;
using LifeOS.Domain.Common;

namespace LifeOS.Domain.Budget;

/// <summary>Months of the budget are their first day (ADR 0020).</summary>
public static class BudgetMonths
{
    public static DateOnly Of(DateOnly day) => new(day.Year, day.Month, 1);

    /// <summary>Reads "2026-11" (or a full date, whose month is taken).</summary>
    public static DateOnly Parse(string? text)
    {
        var value = text?.Trim() ?? "";
        if (DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)) return month;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return Of(day);
        throw new ArgumentException("Mois attendu au format aaaa-mm.");
    }

    public static string Format(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Whole months from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static int Between(DateOnly from, DateOnly to) => (to.Year - from.Year) * 12 + to.Month - from.Month;
}

/// <summary>
/// A line of the budget (ADR 0020): a planned expense (fixed amount or envelope), an extra kept to follow unplanned
/// spending, an off-budget line (transfers between one's own accounts) or an income. Private to its account.
/// </summary>
public sealed class BudgetLine : Entity
{
    public const int MaxNameLength = 100;
    public const int MaxCategoryLength = 60;
    public const int MaxNotesLength = 1000;
    public const string IncomeCategory = "Revenus";
    public const string OffCategory = "Hors budget";

    public static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["fixed"] = "Fixe", ["envelope"] = "Enveloppe", ["extra"] = "Extra", ["off"] = "Hors budget", ["income"] = "Revenu",
    };

    /// <summary>Categories proposed when the owner starts (the postes of the spreadsheet); free afterwards.</summary>
    public static readonly IReadOnlyList<string> DefaultCategories =
    [
        "Logement", "Prêts", "Alimentation", "Transport", "Sorties", "Assurances", "Santé", "Sport", "Abonnements", "Enfants", "Maison",
        "Travaux", "Impôts", "Extras",
    ];

    private readonly List<BudgetAmount> _amounts = [];

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public string Category { get; private set; } = "";
    public string Kind { get; private set; } = "fixed";
    public bool Reduce { get; private set; }
    /// <summary>The planned amount is the monthly cost of the owner's supplements, computed by Health.</summary>
    public bool FromSupplements { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>First month the line no longer counts; null while it runs.</summary>
    public DateOnly? StoppedFrom { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<BudgetAmount> Amounts => _amounts;

    private BudgetLine() { }

    public static BudgetLine Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, string name, string? category, string kind,
        bool reduce, bool fromSupplements, string? notes, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var line = new BudgetLine { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId, CreatedAt = now };
        line.Update(name, category, kind, reduce, fromSupplements, notes, now);
        return line;
    }

    public bool HasAmount => Kind is "fixed" or "envelope" or "income";
    public bool IsExpense => Kind is "fixed" or "envelope" or "extra";

    public void Update(string name, string? category, string kind, bool reduce, bool fromSupplements, string? notes, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength) throw new ArgumentException($"Nom de la ligne obligatoire ({MaxNameLength} caractères maximum).");
        var code = kind?.Trim() ?? "";
        if (!Kinds.ContainsKey(code)) throw new ArgumentException("Type : fixed, envelope, extra, off ou income.");
        var cat = code switch
        {
            "income" => IncomeCategory,
            "off" => OffCategory,
            _ => string.IsNullOrWhiteSpace(category) ? throw new ArgumentException("Poste obligatoire.") : category.Trim(),
        };
        if (cat.Length > MaxCategoryLength) throw new ArgumentException($"Poste : {MaxCategoryLength} caractères maximum.");
        if (code != "income" && code != "off" && (cat == IncomeCategory || cat == OffCategory))
            throw new ArgumentException($"Le poste « {cat} » est réservé à son type de ligne.");
        if (fromSupplements && code is not ("fixed" or "envelope")) throw new ArgumentException("Seule une ligne fixe ou une enveloppe peut suivre le coût des compléments.");
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text?.Length > MaxNotesLength) throw new ArgumentException($"Notes : {MaxNotesLength} caractères maximum.");
        Name = name.Trim();
        Category = cat;
        Kind = code;
        Reduce = code is "income" or "off" ? false : reduce;
        FromSupplements = fromSupplements;
        Notes = text;
        if (!HasAmount) _amounts.Clear();
        UpdatedAt = now;
    }

    /// <summary>
    /// Sets the planned amount from a month on (ADR 0020): earlier months keep what was planned. An income is
    /// planned per month; an expense per month, per year or every N months.
    /// </summary>
    public void SetAmount(DateOnly fromMonth, decimal amount, string? period, int? months, DateTimeOffset now)
    {
        if (!HasAmount) throw new ArgumentException("Une ligne extra ou hors budget n'a pas de montant prévu.");
        var from = BudgetMonths.Of(fromMonth);
        var p = string.IsNullOrWhiteSpace(period) ? "month" : period.Trim();
        if (p is not ("month" or "year" or "months")) throw new ArgumentException("Période : month, year ou months (tous les N mois).");
        if (Kind == "income" && p != "month") throw new ArgumentException("Un revenu est prévu par mois.");
        var n = p switch { "month" => 1, "year" => 12, _ => months ?? 0 };
        if (n is < 1 or > 60) throw new ArgumentException("Tous les N mois : de 1 à 60.");
        if (amount is < 0 or > 10_000_000) throw new ArgumentException("Montant invalide.");
        _amounts.RemoveAll(a => a.FromMonth >= from);
        var current = AmountFor(from);
        if (current is null || current.Amount != amount || current.Period != p || current.Months != n)
            _amounts.Add(new BudgetAmount(from, Math.Round(amount, 2), p, n));
        UpdatedAt = now;
    }

    public BudgetAmount? AmountFor(DateOnly month)
    {
        var m = BudgetMonths.Of(month);
        return _amounts.Where(a => a.FromMonth <= m).MaxBy(a => a.FromMonth);
    }

    public bool ActiveIn(DateOnly month) => StoppedFrom is not { } stop || BudgetMonths.Of(month) < stop;

    /// <summary>Planned for one month, spread to a monthly amount; <paramref name="supplementsMonthlyCost"/> for a line that follows Health.</summary>
    public decimal PlannedFor(DateOnly month, decimal supplementsMonthlyCost)
    {
        if (!HasAmount || !ActiveIn(month)) return 0;
        if (FromSupplements) return supplementsMonthlyCost;
        return AmountFor(month) is { } a ? Math.Round(a.Amount / a.Months, 2) : 0;
    }

    public void Stop(DateOnly fromMonth, DateTimeOffset now)
    {
        StoppedFrom = BudgetMonths.Of(fromMonth);
        UpdatedAt = now;
    }

    public void Resume(DateTimeOffset now)
    {
        StoppedFrom = null;
        UpdatedAt = now;
    }
}

/// <summary>A planned amount from a month on: per month (1), per year (12) or every N months.</summary>
public sealed record BudgetAmount(DateOnly FromMonth, decimal Amount, string Period, int Months);
