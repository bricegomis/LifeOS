using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

public sealed class SportTemplate : Entity
{
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";
    public string Sport { get; private set; } = "";
    public int DurationMinutes { get; private set; }
    public decimal? DistanceKm { get; private set; }
    public string Intensity { get; private set; } = "";
    public decimal Calories { get; private set; }
    public bool IsArchived { get; private set; }

    private SportTemplate() { }

    public static SportTemplate Create(Guid householdId, string name, string sport, int durationMinutes,
        decimal? distanceKm, string intensity, decimal calories)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        var template = new SportTemplate { Id = Guid.NewGuid(), HouseholdId = householdId };
        template.Update(name, sport, durationMinutes, distanceKm, intensity, calories);
        return template;
    }

    public static void Validate(string name, string sport, int durationMinutes, decimal? distanceKm,
        string intensity, decimal calories)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200
            || string.IsNullOrWhiteSpace(sport) || sport.Length > 50)
            throw new ArgumentException("Nom et sport obligatoires (200 / 50 caractères maximum).");
        if (durationMinutes <= 0 || durationMinutes > 1440) throw new ArgumentException("Durée : 1 à 1440 minutes.");
        if (distanceKm < 0 || calories < 0) throw new ArgumentException("Distance et calories non négatives.");
        if (intensity is not ("low" or "moderate" or "high")) throw new ArgumentException("Intensité : low, moderate ou high.");
    }

    public void Update(string name, string sport, int durationMinutes, decimal? distanceKm,
        string intensity, decimal calories)
    {
        Validate(name, sport, durationMinutes, distanceKm, intensity, calories);
        Name = name.Trim();
        Sport = sport.Trim();
        DurationMinutes = durationMinutes;
        DistanceKm = distanceKm;
        Intensity = intensity;
        Calories = calories;
    }

    public void Archive() => IsArchived = true;
}
