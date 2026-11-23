using LifeOS.Domain.Health;

namespace LifeOS.Application.Health.Contracts;

/// <summary>Reads the Health read contract needs; implemented by Infrastructure on the health tables.</summary>
public interface IHealthReadData
{
    /// <summary>Days imported from Garmin for one account of the household, from <paramref name="from"/> to <paramref name="to"/> included.</summary>
    Task<IReadOnlyList<DailyWellness>> WellnessAsync(Guid householdId, Guid ownerUserId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Supplements of one account that are not stopped.</summary>
    Task<IReadOnlyList<Supplement>> ActiveSupplementsAsync(Guid householdId, Guid ownerUserId, CancellationToken ct);
}

/// <summary>Sleep and recovery of one night and day, as Garmin reported it; every value may be missing.</summary>
/// <param name="HrvStatus">balanced, unbalanced, low or poor.</param>
public sealed record SleepRecoveryDayDto(DateOnly Date, Guid MemberProfileId, DateTimeOffset? SleepStart, DateTimeOffset? SleepEnd,
    int? SleepSeconds, int? SleepScore, string? SleepQuality, int? RestingHeartRate, int? HrvNightAverage, string? HrvStatus,
    int? BodyBatteryWake, int? StressAverage);

/// <summary>
/// Published read contract of the Health module (ADR 0009, ADR 0018). Health stays private to its account: the
/// future planner only reads sleep and recovery imported from Garmin (ADR 0019), for the account it plans for; the
/// Budget (ADR 0020) reads the monthly cost of the account's own supplements. What else the planner may read is
/// decided when the planner is scoped.
/// </summary>
public sealed class HealthReadContract(IHealthReadData data, TimeProvider clock)
{
    private const int MaxDays = 366;

    public DateOnly Today() => HealthClock.Today(clock);

    public async Task<IReadOnlyList<SleepRecoveryDayDto>> SleepAndRecoveryAsync(Guid householdId, Guid ownerUserId, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxDays) throw new ArgumentException($"Période : 1 à {MaxDays} jours.");
        return (await data.WellnessAsync(householdId, ownerUserId, from, to, ct)).OrderBy(w => w.Date)
            .Select(w => new SleepRecoveryDayDto(w.Date, w.MemberProfileId, w.SleepStart, w.SleepEnd, w.SleepSeconds, w.SleepScore,
                w.SleepQuality, w.RestingHeartRate, w.HrvNightAverage, w.HrvStatus, w.BodyBatteryWake, w.StressAverage))
            .ToList();
    }

    /// <summary>Monthly cost of the account's supplements that are not stopped (cures smoothed); those without a price count 0.</summary>
    public async Task<decimal> SupplementsMonthlyCostAsync(Guid householdId, Guid ownerUserId, CancellationToken ct) =>
        (await data.ActiveSupplementsAsync(householdId, ownerUserId, ct)).Sum(s => s.MonthlyCost ?? 0);
}
