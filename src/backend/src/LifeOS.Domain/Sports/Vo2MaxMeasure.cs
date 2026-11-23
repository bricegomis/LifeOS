using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// VO2max estimated by the watch on one day for one sport (ADR 0019), imported from Garmin only.
/// Garmin computes a running value after an outdoor run, a cycling one only with a power meter.
/// </summary>
public sealed class Vo2MaxMeasure : Entity
{
    public static readonly IReadOnlyDictionary<string, string> Sports = new Dictionary<string, string>
    {
        ["running"] = "Course",
        ["cycling"] = "Vélo",
    };

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Sport { get; private set; } = "";
    /// <summary>ml/kg/min, one decimal (Garmin's precise value).</summary>
    public decimal Value { get; private set; }

    private Vo2MaxMeasure() { }

    public static Vo2MaxMeasure Create(Guid householdId, Guid memberProfileId, DateOnly date, string sport, decimal value, DateOnly today)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        var measure = new Vo2MaxMeasure { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId };
        measure.Set(date, sport, value, today);
        return measure;
    }

    public void Set(DateOnly date, string sport, decimal value, DateOnly today)
    {
        if (date > today.AddDays(1) || date.Year < 2000) throw new ArgumentException("Date de VO2max invalide.");
        if (!Sports.ContainsKey(sport)) throw new ArgumentException("VO2max : running ou cycling.");
        if (value is < 10 or > 100) throw new ArgumentException("VO2max : 10 à 100 ml/kg/min.");
        Date = date;
        Sport = sport;
        Value = Math.Round(value, 1);
    }
}
