using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// A daily habit with a number (ADR 0013): "100 pompes", "20 min de mobilité". Filled by
/// cumulated entries during the day; no calories, not a session. Its target and jokers change
/// from a given day, so past days are judged with the rule they had.
/// </summary>
public sealed class DailyChallenge : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Units = new Dictionary<string, string>
    {
        ["reps"] = "Répétitions",
        ["minutes"] = "Minutes",
    };

    public const int MaxJokersPerWeek = 2;

    private readonly List<ChallengeRule> _rules = [];

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public string Unit { get; private set; } = "";
    public DateOnly StartedOn { get; private set; }
    /// <summary>Last day counted; null while the challenge runs.</summary>
    public DateOnly? StoppedOn { get; private set; }
    public IReadOnlyList<ChallengeRule> Rules => _rules;

    private DailyChallenge() { }

    public static DailyChallenge Create(Guid householdId, Guid memberProfileId, string name, string unit,
        int dailyTarget, int jokersPerWeek, DateOnly today)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        var code = unit?.Trim() ?? "";
        if (!Units.ContainsKey(code)) throw new ArgumentException("Unité : reps (répétitions) ou minutes.");
        var challenge = new DailyChallenge
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId,
            Unit = code, StartedOn = today,
        };
        challenge.Rename(name);
        challenge._rules.Add(ChallengeRule.Create(today, dailyTarget, jokersPerWeek));
        return challenge;
    }

    public bool IsActive => StoppedOn is null;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Nom du défi obligatoire (100 caractères maximum).");
        Name = name.Trim();
    }

    /// <summary>The rule in force on a day (the first one before the start).</summary>
    public ChallengeRule RuleOn(DateOnly day) =>
        _rules.Where(rule => rule.ValidFrom <= day).MaxBy(rule => rule.ValidFrom) ?? _rules.MinBy(rule => rule.ValidFrom)!;

    /// <summary>A new target or joker count applies from today; past days keep theirs.</summary>
    public void ChangeRule(int dailyTarget, int jokersPerWeek, DateOnly today)
    {
        if (!IsActive) throw new ArgumentException("Ce défi est arrêté.");
        var current = RuleOn(today);
        if (current.DailyTarget == dailyTarget && current.JokersPerWeek == jokersPerWeek) return;
        var changed = ChallengeRule.Create(today, dailyTarget, jokersPerWeek);
        // A rule already starting today is edited in place (same key), otherwise a new one starts today.
        if (_rules.SingleOrDefault(rule => rule.ValidFrom == today) is { } todays) todays.CopyFrom(changed);
        else _rules.Add(changed);
    }

    /// <summary>Stops counting after today; the history stays.</summary>
    public void Stop(DateOnly today)
    {
        if (IsActive) StoppedOn = today < StartedOn ? StartedOn : today;
    }

    public ChallengeEntry Log(DateOnly day, int quantity, DateOnly today, DateTimeOffset now)
    {
        if (!IsActive) throw new ArgumentException("Ce défi est arrêté.");
        if (day > today) throw new ArgumentException("Impossible d'ajouter une série dans le futur.");
        if (day < StartedOn) throw new ArgumentException("Ce jour est avant le début du défi.");
        return ChallengeEntry.Create(HouseholdId, Id, day, quantity, now);
    }
}

/// <summary>Target and jokers of a challenge from a day on.</summary>
public sealed class ChallengeRule
{
    public DateOnly ValidFrom { get; private set; }
    public int DailyTarget { get; private set; }
    public int JokersPerWeek { get; private set; }

    private ChallengeRule() { }

    public static ChallengeRule Create(DateOnly validFrom, int dailyTarget, int jokersPerWeek)
    {
        if (dailyTarget is < 1 or > 100000) throw new ArgumentException("Cible par jour : 1 à 100000.");
        if (jokersPerWeek is < 0 or > DailyChallenge.MaxJokersPerWeek) throw new ArgumentException("Jokers par semaine : 0 à 2.");
        return new ChallengeRule { ValidFrom = validFrom, DailyTarget = dailyTarget, JokersPerWeek = jokersPerWeek };
    }

    internal void CopyFrom(ChallengeRule other)
    {
        DailyTarget = other.DailyTarget;
        JokersPerWeek = other.JokersPerWeek;
    }
}

/// <summary>One set added to a challenge ("+20"). The day's total is the sum of its entries.</summary>
public sealed class ChallengeEntry : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid ChallengeId { get; private set; }
    public DateOnly Date { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset LoggedAt { get; private set; }

    private ChallengeEntry() { }

    internal static ChallengeEntry Create(Guid householdId, Guid challengeId, DateOnly date, int quantity, DateTimeOffset now)
    {
        if (quantity is < 1 or > 10000) throw new ArgumentException("Quantité : 1 à 10000.");
        return new ChallengeEntry { Id = Guid.NewGuid(), HouseholdId = householdId, ChallengeId = challengeId, Date = date, Quantity = quantity, LoggedAt = now };
    }
}
