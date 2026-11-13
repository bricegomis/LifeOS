using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// What one profile wants to do in a week (ADR 0013), instead of dated expected sessions.
/// Progress is computed on read. A goal is valid from a Monday, until an exclusive Monday once
/// stopped or replaced, so past weeks keep the goal they had.
/// </summary>
public sealed class WeeklyGoal : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["template"] = "Par séance type",
        ["sport"] = "Par sport",
        ["sessions"] = "Nombre de séances",
        ["volume"] = "Volume",
    };

    public static readonly IReadOnlyDictionary<string, string> Units = new Dictionary<string, string>
    {
        ["kcal"] = "Calories (kcal)",
        ["km"] = "Distance (km)",
        ["hours"] = "Durée (heures)",
    };

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Kind { get; private set; } = "";
    public Guid? TemplateId { get; private set; }
    public string? Sport { get; private set; }
    public string? Unit { get; private set; }
    public decimal Target { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }

    private WeeklyGoal() { }

    public static WeeklyGoal Create(Guid householdId, Guid memberProfileId, WeeklyGoalValues values, DateOnly today)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        var goal = new WeeklyGoal
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId,
            ValidFrom = SportWeek.Monday(today),
        };
        goal.Apply(values);
        return goal;
    }

    public bool IsActive => ValidUntil is null;

    public bool AppliesTo(DateOnly monday) => ValidFrom <= monday && (ValidUntil is null || monday < ValidUntil);

    /// <summary>
    /// Changes the goal from the current week. A goal created this week is edited in place;
    /// an older one is closed and a new goal is returned, so past weeks keep their goal.
    /// </summary>
    public WeeklyGoal? ChangeFrom(DateOnly today, WeeklyGoalValues values)
    {
        if (!IsActive) throw new ArgumentException("Cet objectif est arrêté.");
        var monday = SportWeek.Monday(today);
        if (ValidFrom >= monday) { Apply(values); return null; }
        ValidUntil = monday;
        return Create(HouseholdId, MemberProfileId, values, today);
    }

    /// <summary>
    /// Stops the goal: it still counts for the current week and disappears from the next ones.
    /// Returns false when the goal started this week and can simply be deleted.
    /// </summary>
    public bool Stop(DateOnly today)
    {
        if (!IsActive) return true;
        var monday = SportWeek.Monday(today);
        if (ValidFrom >= monday) return false;
        ValidUntil = monday.AddDays(7);
        return true;
    }

    private void Apply(WeeklyGoalValues values)
    {
        var kind = values.Kind?.Trim() ?? "";
        if (!Kinds.ContainsKey(kind))
            throw new ArgumentException("Forme d'objectif : template (séance type), sport, sessions (nombre de séances) ou volume.");
        TemplateId = null; Sport = null; Unit = null;
        switch (kind)
        {
            case "template":
                TemplateId = values.TemplateId ?? throw new ArgumentException("Choisissez la séance type de l'objectif.");
                break;
            case "sport":
                Sport = SportCatalog.Require(values.Sport);
                break;
            case "volume":
                var unit = values.Unit?.Trim() ?? "";
                Unit = Units.ContainsKey(unit) ? unit : throw new ArgumentException("Unité : kcal, km ou hours.");
                break;
        }
        if (kind == "volume")
        {
            if (values.Target is <= 0 or > 100000) throw new ArgumentException("Cible : plus de 0 et au plus 100000.");
        }
        else if (values.Target is < 1 or > 21 || values.Target != decimal.Truncate(values.Target))
        {
            throw new ArgumentException("Nombre de séances par semaine : entier de 1 à 21.");
        }
        Kind = kind;
        Target = values.Target;
    }
}

public sealed record WeeklyGoalValues(string Kind, Guid? TemplateId, string? Sport, string? Unit, decimal Target);
