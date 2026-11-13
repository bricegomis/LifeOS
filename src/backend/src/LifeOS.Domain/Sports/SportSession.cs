using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// A session actually done, on a date, by one profile (ADR 0013). Only logged sessions count:
/// there are no expected sessions to confirm. Calories are typed, never computed; null means
/// unknown and makes the week's total partial.
/// </summary>
public sealed class SportSession : Entity
{
    public const string ManualSource = "manual";

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Sport { get; private set; } = "";
    /// <summary>Template the values were copied from, kept to count template goals.</summary>
    public Guid? TemplateId { get; private set; }
    public int DurationMinutes { get; private set; }
    public decimal? DistanceKm { get; private set; }
    public int? Calories { get; private set; }
    /// <summary>1 (very hard) to 10 (excellent).</summary>
    public int? Feeling { get; private set; }
    public int? AverageHeartRate { get; private set; }
    public int? MaxHeartRate { get; private set; }
    /// <summary>Free text: exercises, sets, loads, WOD, score.</summary>
    public string Notes { get; private set; } = "";
    /// <summary>Where the session comes from; <c>manual</c> until an import exists.</summary>
    public string Source { get; private set; } = ManualSource;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private SportSession() { }

    public static SportSession Create(Guid householdId, Guid memberProfileId, SportSessionValues values,
        DateOnly today, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        var session = new SportSession { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId, CreatedAt = now };
        session.Update(values, today, now);
        return session;
    }

    public void Update(SportSessionValues values, DateOnly today, DateTimeOffset now)
    {
        if (values.Date > today) throw new ArgumentException("Une séance réalisée ne peut pas être dans le futur.");
        if (values.Date.Year < 2000) throw new ArgumentException("Date invalide.");
        if (values.Feeling is < 1 or > 10) throw new ArgumentException("Ressenti : 1 à 10.");
        if (values.AverageHeartRate is < 30 or > 250 || values.MaxHeartRate is < 30 or > 250)
            throw new ArgumentException("Fréquence cardiaque : 30 à 250 bpm.");
        if (values.AverageHeartRate > values.MaxHeartRate)
            throw new ArgumentException("La fréquence cardiaque moyenne dépasse la maximale.");
        var notes = values.Notes?.Trim() ?? "";
        if (notes.Length > 4000) throw new ArgumentException("Notes : 4000 caractères maximum.");

        Sport = SportCatalog.Require(values.Sport);
        Date = values.Date;
        TemplateId = values.TemplateId;
        DurationMinutes = SportCatalog.CheckDuration(values.DurationMinutes);
        DistanceKm = SportCatalog.CheckDistance(Sport, values.DistanceKm);
        Calories = SportCatalog.CheckCalories(values.Calories);
        Feeling = values.Feeling;
        AverageHeartRate = values.AverageHeartRate;
        MaxHeartRate = values.MaxHeartRate;
        Notes = notes;
        UpdatedAt = now;
    }
}

public sealed record SportSessionValues(
    DateOnly Date,
    string Sport,
    Guid? TemplateId,
    int DurationMinutes,
    decimal? DistanceKm,
    int? Calories,
    int? Feeling,
    int? AverageHeartRate,
    int? MaxHeartRate,
    string? Notes);
