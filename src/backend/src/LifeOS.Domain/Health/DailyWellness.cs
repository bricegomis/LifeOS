using LifeOS.Domain.Common;

namespace LifeOS.Domain.Health;

/// <summary>
/// One day of sleep and recovery imported from Garmin (ADR 0019): the night ending that day, resting
/// heart rate, HRV, Body Battery, stress, steps. The columns are those drawn as curves; Garmin's raw
/// answers of the day are kept beside them so nothing is lost. Private to its owner like all health
/// data; the planning agent may read it (ADR 0019 exception).
/// </summary>
public sealed class DailyWellness : Entity
{
    public static readonly IReadOnlyDictionary<string, string> HrvStatuses = new Dictionary<string, string>
    {
        ["balanced"] = "Équilibré",
        ["unbalanced"] = "Déséquilibré",
        ["low"] = "Bas",
        ["poor"] = "Faible",
    };

    public Guid HouseholdId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public DateOnly Date { get; private set; }

    public DateTimeOffset? SleepStart { get; private set; }
    public DateTimeOffset? SleepEnd { get; private set; }
    public int? SleepSeconds { get; private set; }
    public int? DeepSleepSeconds { get; private set; }
    public int? LightSleepSeconds { get; private set; }
    public int? RemSleepSeconds { get; private set; }
    public int? AwakeSeconds { get; private set; }
    public int? SleepScore { get; private set; }
    /// <summary>Garmin's word for the score: POOR, FAIR, GOOD, EXCELLENT.</summary>
    public string? SleepQuality { get; private set; }

    public int? RestingHeartRate { get; private set; }
    public int? HrvNightAverage { get; private set; }
    public int? HrvWeeklyAverage { get; private set; }
    public int? HrvBaselineLow { get; private set; }
    public int? HrvBaselineHigh { get; private set; }
    public string? HrvStatus { get; private set; }
    public decimal? Respiration { get; private set; }
    public decimal? SpO2 { get; private set; }

    public int? BodyBatteryWake { get; private set; }
    public int? BodyBatteryHigh { get; private set; }
    public int? BodyBatteryLow { get; private set; }
    public int? StressAverage { get; private set; }
    public int? Steps { get; private set; }

    /// <summary>Garmin's raw answers of the day (JSON object by endpoint).</summary>
    public string? Raw { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DailyWellness() { }

    public static DailyWellness Create(Guid householdId, Guid ownerUserId, Guid memberProfileId, DateOnly date)
    {
        if (householdId == Guid.Empty || ownerUserId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        if (date.Year < 2000) throw new ArgumentException("Date invalide.");
        return new DailyWellness { Id = Guid.NewGuid(), HouseholdId = householdId, OwnerUserId = ownerUserId, MemberProfileId = memberProfileId, Date = date };
    }

    /// <summary>Replaces every value with Garmin's (a value Garmin no longer gives becomes empty).</summary>
    public void Apply(DailyWellnessValues v, DateTimeOffset now)
    {
        static int? Range(int? x, int min, int max, string label) =>
            x is null || (x >= min && x <= max) ? x : throw new ArgumentException($"{label} hors limites.");
        const int day = 86400;
        SleepStart = v.SleepStart?.ToUniversalTime();
        SleepEnd = v.SleepEnd?.ToUniversalTime();
        if (SleepStart is { } s && SleepEnd is { } e && e <= s) throw new ArgumentException("Le réveil doit suivre le coucher.");
        SleepSeconds = Range(v.SleepSeconds, 0, day, "Sommeil");
        DeepSleepSeconds = Range(v.DeepSleepSeconds, 0, day, "Sommeil profond");
        LightSleepSeconds = Range(v.LightSleepSeconds, 0, day, "Sommeil léger");
        RemSleepSeconds = Range(v.RemSleepSeconds, 0, day, "Sommeil paradoxal");
        AwakeSeconds = Range(v.AwakeSeconds, 0, day, "Éveil");
        SleepScore = Range(v.SleepScore, 0, 100, "Score de sommeil");
        SleepQuality = string.IsNullOrWhiteSpace(v.SleepQuality) ? null : v.SleepQuality.Trim().ToUpperInvariant()[..Math.Min(20, v.SleepQuality.Trim().Length)];
        RestingHeartRate = Range(v.RestingHeartRate, 20, 200, "FC au repos");
        HrvNightAverage = Range(v.HrvNightAverage, 1, 400, "HRV");
        HrvWeeklyAverage = Range(v.HrvWeeklyAverage, 1, 400, "HRV de la semaine");
        HrvBaselineLow = Range(v.HrvBaselineLow, 1, 400, "Plage HRV");
        HrvBaselineHigh = Range(v.HrvBaselineHigh, 1, 400, "Plage HRV");
        var status = v.HrvStatus?.Trim().ToLowerInvariant();
        HrvStatus = string.IsNullOrEmpty(status) ? null : HrvStatuses.ContainsKey(status) ? status : null;
        Respiration = v.Respiration is null or (>= 0 and <= 80) ? v.Respiration : throw new ArgumentException("Respiration hors limites.");
        SpO2 = v.SpO2 is null or (>= 0 and <= 100) ? v.SpO2 : throw new ArgumentException("SpO2 hors limites.");
        BodyBatteryWake = Range(v.BodyBatteryWake, 0, 100, "Body Battery");
        BodyBatteryHigh = Range(v.BodyBatteryHigh, 0, 100, "Body Battery");
        BodyBatteryLow = Range(v.BodyBatteryLow, 0, 100, "Body Battery");
        StressAverage = Range(v.StressAverage, 0, 100, "Stress");
        Steps = Range(v.Steps, 0, 500_000, "Pas");
        Raw = v.Raw;
        UpdatedAt = now;
    }
}

public sealed record DailyWellnessValues(
    DateTimeOffset? SleepStart, DateTimeOffset? SleepEnd, int? SleepSeconds, int? DeepSleepSeconds, int? LightSleepSeconds,
    int? RemSleepSeconds, int? AwakeSeconds, int? SleepScore, string? SleepQuality,
    int? RestingHeartRate, int? HrvNightAverage, int? HrvWeeklyAverage, int? HrvBaselineLow, int? HrvBaselineHigh, string? HrvStatus,
    decimal? Respiration, decimal? SpO2, int? BodyBatteryWake, int? BodyBatteryHigh, int? BodyBatteryLow, int? StressAverage, int? Steps,
    string? Raw);
