using LifeOS.Application.Sports.Contracts;
using LifeOS.Application.Tests.Food;
using LifeOS.Domain.Sports;

namespace LifeOS.Application.Tests.Sports;

public sealed class SportReadContractTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    // 2026-11-12 23:30 UTC is already Friday 13 in Paris.
    private static readonly FixedClock LateEvening = new(new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero));
    private static readonly DateOnly Friday = new(2026, 11, 13);

    private sealed class FakeData : ISportReadData
    {
        public List<SportSession> Sessions { get; } = [];
        public List<WeeklyGoal> Goals { get; } = [];
        public List<DailyChallenge> Challenges { get; } = [];
        public List<ChallengeEntry> Entries { get; } = [];
        public Dictionary<Guid, string> Names { get; } = [];

        public Task<IReadOnlyList<SportSession>> SessionsAsync(Guid householdId, Guid? profileId, DateOnly from, DateOnly to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SportSession>>(Sessions.Where(s => s.HouseholdId == householdId && (profileId == null || s.MemberProfileId == profileId)
                && s.Date >= from && s.Date <= to).ToList());
        public Task<IReadOnlyList<WeeklyGoal>> GoalsAsync(Guid householdId, Guid profileId, DateOnly fromMonday, DateOnly toMonday, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WeeklyGoal>>(Goals.Where(g => g.HouseholdId == householdId && g.MemberProfileId == profileId).ToList());
        public Task<IReadOnlyDictionary<Guid, string>> TemplateNamesAsync(Guid householdId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(Names);
        public Task<IReadOnlyList<DailyChallenge>> ChallengesAsync(Guid householdId, Guid profileId, bool activeOnly, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DailyChallenge>>(Challenges.Where(c => c.MemberProfileId == profileId && (!activeOnly || c.IsActive)).ToList());
        public Task<IReadOnlyDictionary<Guid, Dictionary<DateOnly, int>>> DayTotalsAsync(Guid householdId, IReadOnlyCollection<Guid> challengeIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Dictionary<DateOnly, int>>>(Entries.Where(e => challengeIds.Contains(e.ChallengeId))
                .GroupBy(e => e.ChallengeId).ToDictionary(g => g.Key, g => g.GroupBy(e => e.Date).ToDictionary(d => d.Key, d => d.Sum(e => e.Quantity))));
        public Task<IReadOnlyList<ChallengeEntry>> EntriesAsync(Guid householdId, IReadOnlyCollection<Guid> challengeIds, DateOnly day, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ChallengeEntry>>(Entries.Where(e => challengeIds.Contains(e.ChallengeId) && e.Date == day).ToList());
    }

    private static SportSession Session(Guid profile, DateOnly date, Guid? template, int? calories) =>
        SportSession.Create(Household, profile, new SportSessionValues(date, "running", template, 45, 8, calories, null, null, null, null),
            Friday, LateEvening.GetUtcNow());

    [Fact]
    public void The_sport_day_is_the_day_in_Paris() =>
        Assert.Equal(Friday, new SportReadContract(new FakeData(), LateEvening).Today());

    [Fact]
    public async Task Week_reports_are_per_profile_with_readable_goal_labels()
    {
        var data = new FakeData();
        var footing = Guid.NewGuid();
        data.Names[footing] = "Footing 45 min";
        data.Sessions.Add(Session(Profile, Friday.AddDays(-3), footing, 520));
        data.Sessions.Add(Session(Profile, Friday.AddDays(-10), footing, 500));
        data.Sessions.Add(Session(Other, Friday.AddDays(-2), footing, 999));
        data.Goals.Add(WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("template", footing, null, null, 2), Friday.AddDays(-7)));
        data.Goals.Add(WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("volume", null, null, "kcal", 1500), Friday));

        var weeks = await new SportReadContract(data, LateEvening).WeeksAsync(Household, Profile, Friday, 2, CancellationToken.None);

        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 9)], weeks.Select(w => w.Monday));
        Assert.Single(weeks[0].Goals);
        Assert.Equal(["Footing 45 min ×2", "1500 kcal"], weeks[1].Goals.Select(g => g.Goal.Label));
        Assert.Equal(520, weeks[1].Calories);
        Assert.Equal("Footing 45 min", Assert.Single(weeks[1].Sessions).TemplateName);
    }

    [Fact]
    public async Task Challenge_reports_carry_today_entries_and_calendar()
    {
        var data = new FakeData();
        var pushups = DailyChallenge.Create(Household, Profile, "Pompes", "reps", 100, 1, Friday.AddDays(-2));
        data.Challenges.Add(pushups);
        data.Entries.Add(pushups.Log(Friday.AddDays(-2), 100, Friday, LateEvening.GetUtcNow()));
        data.Entries.Add(pushups.Log(Friday, 25, Friday, LateEvening.GetUtcNow()));
        data.Entries.Add(pushups.Log(Friday, 20, Friday, LateEvening.GetUtcNow()));

        var report = Assert.Single(await new SportReadContract(data, LateEvening).ChallengesAsync(Household, Profile, 1, false, CancellationToken.None));

        Assert.Equal(45, report.TodayTotal);
        Assert.False(report.ReachedToday);
        Assert.Equal(2, report.TodayEntries.Count);
        Assert.Equal(1, report.Streak);
        Assert.Equal(["not_counted", "not_counted", "reached", "joker", "in_progress"], report.Calendar.Select(d => d.Status));
        Assert.Equal(145, report.Total);
    }

    [Fact]
    public async Task Sport_days_group_sessions_per_profile_and_flag_unknown_calories()
    {
        var data = new FakeData();
        data.Sessions.Add(Session(Profile, Friday, null, 300));
        data.Sessions.Add(Session(Profile, Friday, null, null));
        data.Sessions.Add(Session(Other, Friday, null, 200));

        var days = await new SportReadContract(data, LateEvening).DaysAsync(Household, Friday, Friday, CancellationToken.None);

        Assert.Equal(2, days.Count);
        var mine = days.Single(d => d.MemberProfileId == Profile);
        Assert.Equal(300, mine.Calories);
        Assert.False(mine.CaloriesComplete);
    }
}
