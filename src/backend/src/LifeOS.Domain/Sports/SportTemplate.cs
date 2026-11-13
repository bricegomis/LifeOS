using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// A reusable session (ADR 0013): its values are copied into a logged session, which keeps its
/// own values. Editing a template never rewrites past sessions; templates are archived, not deleted.
/// </summary>
public sealed class SportTemplate : Entity
{
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";
    public string Sport { get; private set; } = "";
    public int DurationMinutes { get; private set; }
    public decimal? DistanceKm { get; private set; }
    /// <summary>Suggested calories, typed by hand; copied into the session, never computed.</summary>
    public int Calories { get; private set; }
    public bool IsArchived { get; private set; }

    private SportTemplate() { }

    public static SportTemplate Create(Guid householdId, string name, string sport, int durationMinutes,
        decimal? distanceKm, int calories)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        var template = new SportTemplate { Id = Guid.NewGuid(), HouseholdId = householdId };
        template.Update(name, sport, durationMinutes, distanceKm, calories);
        return template;
    }

    public void Update(string name, string sport, int durationMinutes, decimal? distanceKm, int calories)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Nom obligatoire (200 caractères maximum).");
        Sport = SportCatalog.Require(sport);
        Name = name.Trim();
        DurationMinutes = SportCatalog.CheckDuration(durationMinutes);
        DistanceKm = SportCatalog.CheckDistance(Sport, distanceKm);
        Calories = SportCatalog.CheckCalories(calories) ?? 0;
    }

    public void Archive() => IsArchived = true;
}
