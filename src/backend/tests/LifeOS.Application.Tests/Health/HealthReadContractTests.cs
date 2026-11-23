using LifeOS.Application.Health.Contracts;
using LifeOS.Application.Tests.Food;
using LifeOS.Domain.Health;

namespace LifeOS.Application.Tests.Health;

public sealed class HealthReadContractTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 11, 23);

    private sealed class FakeData : IHealthReadData
    {
        public List<DailyWellness> Days { get; } = [];

        public Task<IReadOnlyList<DailyWellness>> WellnessAsync(Guid householdId, Guid ownerUserId, DateOnly from, DateOnly to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DailyWellness>>(Days.Where(d => d.HouseholdId == householdId && d.OwnerUserId == ownerUserId
                && d.Date >= from && d.Date <= to).ToList());

        public List<Supplement> Supplements { get; } = [];

        public Task<IReadOnlyList<Supplement>> ActiveSupplementsAsync(Guid householdId, Guid ownerUserId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Supplement>>(Supplements.Where(s => s.HouseholdId == householdId && s.OwnerUserId == ownerUserId && !s.IsStopped).ToList());
    }

    private static DailyWellness Day(Guid owner, DateOnly date, int sleepSeconds, int bodyBattery)
    {
        var day = DailyWellness.Create(Household, owner, Profile, date);
        day.Apply(new DailyWellnessValues(null, null, sleepSeconds, null, null, null, null, 80, "GOOD", 52, 60, null, null, null, "balanced",
            null, null, bodyBattery, null, null, 25, 8000, "{}"), FixedClock.Default.GetUtcNow());
        return day;
    }

    [Fact]
    public async Task Sleep_and_recovery_are_read_for_one_account_by_day()
    {
        var data = new FakeData();
        data.Days.AddRange([Day(Me, Monday, 27000, 70), Day(Me, Monday.AddDays(-1), 21600, 40), Day(Partner, Monday, 30000, 90),
            Day(Me, Monday.AddDays(-10), 25000, 60)]);

        var days = await new HealthReadContract(data, FixedClock.Default).SleepAndRecoveryAsync(Household, Me, Monday.AddDays(-1), Monday,
            CancellationToken.None);

        Assert.Equal([(Monday.AddDays(-1), 21600, 40), (Monday, 27000, 70)], days.Select(d => (d.Date, d.SleepSeconds!.Value, d.BodyBatteryWake!.Value)));
        Assert.Equal(("balanced", 52, 80), (days[0].HrvStatus, days[0].RestingHeartRate, days[0].SleepScore));
        await Assert.ThrowsAsync<ArgumentException>(() => new HealthReadContract(data, FixedClock.Default)
            .SleepAndRecoveryAsync(Household, Me, Monday, Monday.AddDays(-1), CancellationToken.None));
    }

    [Fact]
    public async Task The_budget_reads_the_monthly_cost_of_the_account_s_active_supplements_only()
    {
        var data = new FakeData();
        Supplement Make(Guid owner, decimal? price) => Supplement.Create(Household, owner, Profile, "Magnésium", null, 2, "gelule", "soir", 180, price, null, null,
            SupplementPeriod.AllYear(Monday), FixedClock.Default.GetUtcNow());
        var stopped = Make(Me, 50);
        stopped.Stop(FixedClock.Default.GetUtcNow());
        data.Supplements.AddRange([Make(Me, 29.9m), Make(Me, null), Make(Partner, 100), stopped]);

        Assert.Equal(9.97m, await new HealthReadContract(data, FixedClock.Default).SupplementsMonthlyCostAsync(Household, Me, CancellationToken.None));
    }
}
