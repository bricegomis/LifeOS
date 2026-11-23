using LifeOS.Domain.Common;

namespace LifeOS.Domain.Sports;

/// <summary>
/// A session actually done by one profile, with its start and end time (ADR 0013, ADR 0019). Only
/// logged sessions count: there are no expected sessions to confirm. Calories are typed or measured
/// by the watch, never computed; null means unknown and makes the week's total partial.
/// A session comes from a manual entry or from Garmin; Garmin values are overwritten at each sync,
/// the LifeOS notes and template never are, and the sport is kept once changed in LifeOS.
/// </summary>
public sealed class SportSession : Entity
{
    public const string ManualSource = "manual";
    public const string GarminSource = "garmin";

    /// <summary>The five feelings asked by the watch at the end of a session, weakest first.</summary>
    public static readonly IReadOnlyDictionary<string, string> Feels = new Dictionary<string, string>
    {
        ["very_weak"] = "Très faible",
        ["weak"] = "Faible",
        ["normal"] = "Normale",
        ["strong"] = "Forte",
        ["very_strong"] = "Très forte",
    };

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    /// <summary>Paris day of the start, kept for week queries.</summary>
    public DateOnly Date { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset EndedAt { get; private set; }
    public string Sport { get; private set; } = "";
    /// <summary>Template the values were copied from, kept to count template goals.</summary>
    public Guid? TemplateId { get; private set; }
    /// <summary>Active time; for Garmin the timer time (pauses excluded).</summary>
    public int DurationMinutes { get; private set; }
    public decimal? DistanceKm { get; private set; }
    public int? Calories { get; private set; }
    /// <summary>Perceived effort, 1 (very easy) to 10 (maximal).</summary>
    public int? Effort { get; private set; }
    /// <summary>One of <see cref="Feels"/>.</summary>
    public string? Feel { get; private set; }
    public int? AverageHeartRate { get; private set; }
    public int? MaxHeartRate { get; private set; }
    /// <summary>Free text: exercises, sets, loads, WOD, score. Never overwritten by an import.</summary>
    public string Notes { get; private set; } = "";
    public string Source { get; private set; } = ManualSource;

    // Values measured by the watch; empty for a manual session.
    public int? ElevationGainMeters { get; private set; }
    public decimal? AverageSpeedKmh { get; private set; }
    public decimal? AerobicTrainingEffect { get; private set; }
    public decimal? AnaerobicTrainingEffect { get; private set; }
    /// <summary>Seconds spent in heart rate zones 1 to 5.</summary>
    public int[]? HeartRateZoneSeconds { get; private set; }
    public long? GarminActivityId { get; private set; }
    public string? GarminType { get; private set; }
    public string? GarminName { get; private set; }
    /// <summary>Garmin's own answer for the activity (JSON), kept for the assistant and later needs.</summary>
    public string? GarminRaw { get; private set; }
    /// <summary>The sport was changed in LifeOS: the next syncs keep it.</summary>
    public bool SportOverridden { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private SportSession() { }

    public bool IsGarmin => Source == GarminSource;

    public static SportSession Create(Guid householdId, Guid memberProfileId, SportSessionValues values,
        DateOnly today, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        var session = new SportSession { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId, CreatedAt = now };
        session.Update(values, today, now);
        return session;
    }

    /// <summary>
    /// Manual change. On a Garmin session only the notes, the template and the sport change: the
    /// watch's values stay those of Garmin.
    /// </summary>
    public void Update(SportSessionValues values, DateOnly today, DateTimeOffset now)
    {
        if (IsGarmin)
        {
            var sport = SportCatalog.Require(values.Sport);
            if (sport != Sport) { DistanceKm = SportCatalog.HasDistance(sport) ? DistanceKm : null; Sport = sport; SportOverridden = true; }
            TemplateId = values.TemplateId;
            Notes = CheckNotes(values.Notes);
            UpdatedAt = now;
            return;
        }
        Apply(values, today);
        UpdatedAt = now;
    }

    /// <summary>
    /// Creates or refreshes the session from a Garmin activity. A manual session turned into a Garmin
    /// one keeps its notes and template.
    /// </summary>
    public void ApplyGarmin(GarminActivityValues g, DateOnly today, DateTimeOffset now)
    {
        var sport = SportOverridden ? Sport : SportCatalog.Require(g.Sport);
        Apply(new SportSessionValues(g.Date, g.StartedAt, g.EndedAt, sport, TemplateId, g.DurationMinutes,
            SportCatalog.HasDistance(sport) ? g.DistanceKm : null, g.Calories, g.Effort, g.Feel,
            g.AverageHeartRate, g.MaxHeartRate, Notes), today);
        if (g.ElevationGainMeters is < 0 or > 20000) throw new ArgumentException("Dénivelé : 0 à 20000 m.");
        if (g.HeartRateZoneSeconds is { Length: not 5 }) throw new ArgumentException("Zones de fréquence cardiaque : 5 valeurs.");
        Source = GarminSource;
        ElevationGainMeters = g.ElevationGainMeters;
        AverageSpeedKmh = g.AverageSpeedKmh is { } speed ? Math.Round(speed, 2) : null;
        AerobicTrainingEffect = g.AerobicTrainingEffect is { } te ? Math.Round(te, 1) : null;
        AnaerobicTrainingEffect = g.AnaerobicTrainingEffect is { } an ? Math.Round(an, 1) : null;
        HeartRateZoneSeconds = g.HeartRateZoneSeconds;
        GarminActivityId = g.ActivityId;
        GarminType = g.Type;
        GarminName = string.IsNullOrWhiteSpace(g.Name) ? null : g.Name.Trim()[..Math.Min(200, g.Name.Trim().Length)];
        GarminRaw = g.Raw;
        UpdatedAt = now;
    }

    public static SportSession FromGarmin(Guid householdId, Guid memberProfileId, GarminActivityValues g, DateOnly today, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || memberProfileId == Guid.Empty) throw new ArgumentException("Foyer et profil obligatoires.");
        var session = new SportSession { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId, CreatedAt = now };
        session.ApplyGarmin(g, today, now);
        return session;
    }

    /// <summary>A manual session of the same sport whose times overlap the activity's (ADR 0019).</summary>
    public bool MatchesGarmin(string sport, DateTimeOffset startedAt, DateTimeOffset endedAt) =>
        !IsGarmin && Sport == sport && StartedAt < endedAt && startedAt < EndedAt;

    private void Apply(SportSessionValues values, DateOnly today)
    {
        if (values.Date > today) throw new ArgumentException("Une séance réalisée ne peut pas être dans le futur.");
        if (values.Date.Year < 2000) throw new ArgumentException("Date invalide.");
        if (values.EndedAt <= values.StartedAt) throw new ArgumentException("La fin de la séance doit suivre son début.");
        if (values.EndedAt - values.StartedAt > TimeSpan.FromHours(24)) throw new ArgumentException("Une séance dure au plus 24 h.");
        if (values.Effort is < 1 or > 10) throw new ArgumentException("Effort perçu : 1 à 10.");
        if (values.Feel is { } feel && !Feels.ContainsKey(feel))
            throw new ArgumentException("Sensation : very_weak, weak, normal, strong ou very_strong.");
        if (values.AverageHeartRate is < 30 or > 250 || values.MaxHeartRate is < 30 or > 250)
            throw new ArgumentException("Fréquence cardiaque : 30 à 250 bpm.");
        if (values.AverageHeartRate > values.MaxHeartRate)
            throw new ArgumentException("La fréquence cardiaque moyenne dépasse la maximale.");

        Sport = SportCatalog.Require(values.Sport);
        Date = values.Date;
        StartedAt = values.StartedAt.ToUniversalTime();
        EndedAt = values.EndedAt.ToUniversalTime();
        TemplateId = values.TemplateId;
        DurationMinutes = SportCatalog.CheckDuration(values.DurationMinutes);
        DistanceKm = SportCatalog.CheckDistance(Sport, values.DistanceKm);
        Calories = SportCatalog.CheckCalories(values.Calories);
        Effort = values.Effort;
        Feel = values.Feel;
        AverageHeartRate = values.AverageHeartRate;
        MaxHeartRate = values.MaxHeartRate;
        Notes = CheckNotes(values.Notes);
    }

    private static string CheckNotes(string? notes)
    {
        var text = notes?.Trim() ?? "";
        return text.Length <= 4000 ? text : throw new ArgumentException("Notes : 4000 caractères maximum.");
    }
}

/// <param name="Date">Paris day of <paramref name="StartedAt"/>.</param>
public sealed record SportSessionValues(
    DateOnly Date,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Sport,
    Guid? TemplateId,
    int DurationMinutes,
    decimal? DistanceKm,
    int? Calories,
    int? Effort,
    string? Feel,
    int? AverageHeartRate,
    int? MaxHeartRate,
    string? Notes);

/// <summary>One Garmin activity, already turned into LifeOS units and codes (<see cref="GarminActivityMapping"/>).</summary>
public sealed record GarminActivityValues(
    long ActivityId,
    string Type,
    string? Name,
    string Sport,
    DateOnly Date,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int DurationMinutes,
    decimal? DistanceKm,
    int? Calories,
    int? Effort,
    string? Feel,
    int? AverageHeartRate,
    int? MaxHeartRate,
    int? ElevationGainMeters,
    decimal? AverageSpeedKmh,
    decimal? AerobicTrainingEffect,
    decimal? AnaerobicTrainingEffect,
    int[]? HeartRateZoneSeconds,
    string? Raw);
