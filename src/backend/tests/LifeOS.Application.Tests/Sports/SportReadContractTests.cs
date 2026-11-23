using LifeOS.Application.Sports;
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
        public List<Routine> Routines { get; } = [];
        public List<Exercise> Exercises { get; } = [];
        public List<ExerciseCompletion> Completions { get; } = [];
        public List<RoutineSession> Passes { get; } = [];
        public SportProfileSettings? Settings { get; set; }

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
        public Task<IReadOnlyList<Routine>> RoutinesAsync(Guid householdId, Guid profileId, bool activeOnly, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Routine>>(Routines.Where(r => r.MemberProfileId == profileId && (!activeOnly || r.IsActive)).ToList());
        public Task<IReadOnlyDictionary<Guid, Exercise>> ExercisesAsync(Guid householdId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Exercise>>(Exercises.ToDictionary(e => e.Id));
        public Task<IReadOnlyList<ExerciseCompletion>> CompletionsAsync(Guid householdId, IReadOnlyCollection<Guid> routineIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExerciseCompletion>>(Completions.Where(c => routineIds.Contains(c.RoutineId)).ToList());
        public Task<IReadOnlyList<RoutineSession>> RoutineSessionsAsync(Guid householdId, IReadOnlyCollection<Guid> routineIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RoutineSession>>(Passes.Where(p => routineIds.Contains(p.RoutineId)).ToList());
        public Task<SportProfileSettings?> SettingsAsync(Guid householdId, Guid profileId, CancellationToken ct) =>
            Task.FromResult(Settings is { } s && s.MemberProfileId == profileId ? s : null);
        public Task<IReadOnlyList<ExerciseCompletion>> ExerciseCompletionsAsync(Guid householdId, Guid profileId, Guid exerciseId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExerciseCompletion>>(Completions.Where(c => c.ExerciseId == exerciseId
                && Routines.Any(r => r.Id == c.RoutineId && r.MemberProfileId == profileId)).ToList());
    }

    private static SportSession Session(Guid profile, DateOnly date, Guid? template, int? calories) =>
        SportSession.Create(Household, profile, new SportSessionValues(date, SportClock.At(date, new TimeOnly(7, 0)), SportClock.At(date, new TimeOnly(7, 45)), "running", template, 45, 8, calories, null, null, null, null, null),
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

    [Fact]
    public async Task Routine_reports_are_per_profile_with_todays_ticks_and_the_exercise_follow_up()
    {
        var data = new FakeData();
        var wallSit = Exercise.Create(Household, "Chaise contre le mur", "Dos plaqué au mur.", "hold", 10);
        var bridge = Exercise.Create(Household, "Pont fessier", null, "reps", null);
        data.Exercises.AddRange([wallSit, bridge]);
        var start = Friday.AddDays(-2);
        var routine = Routine.Create(Household, Profile, "Tendons", Routine.EveryDay, 1,
            [new RoutineLineInput(wallSit, 2, 45, 10), new RoutineLineInput(bridge, 2, 12, null)], start);
        data.Routines.Add(routine);
        data.Routines.Add(Routine.Create(Household, Other, "Dos", Routine.EveryDay, 0, [new RoutineLineInput(bridge, 1, 10, null)], start));
        var now = LateEvening.GetUtcNow();
        RoutineSession Pass(DateOnly day, DateTimeOffset at)
        {
            var pass = RoutineSession.Start(routine, day, at, Friday, false);
            data.Passes.Add(pass);
            return pass;
        }
        foreach (var day in new[] { start, start.AddDays(1) })
        {
            var pass = Pass(day, now.AddDays(day.DayNumber - Friday.DayNumber));
            data.Completions.Add(ExerciseCompletion.Create(routine, pass, wallSit.Id, 2, null, null, now, false));
            data.Completions.Add(ExerciseCompletion.Create(routine, pass, bridge.Id, 2, null, null, now, false));
        }
        // Today: a finished pass, then a second one with one set of the wall sit.
        var morning = Pass(Friday, now.AddHours(-10));
        data.Completions.Add(ExerciseCompletion.Create(routine, morning, wallSit.Id, 2, null, null, now.AddHours(-9), false));
        data.Completions.Add(ExerciseCompletion.Create(routine, morning, bridge.Id, 2, null, null, now.AddHours(-9), false));
        var evening = Pass(Friday, now);
        var tick = ExerciseCompletion.Create(routine, evening, wallSit.Id, 1, 60, 12, now, false);
        tick.SetFeeling(8, 1);
        data.Completions.Add(tick);
        var contract = new SportReadContract(data, LateEvening);

        var report = Assert.Single(await contract.RoutinesAsync(Household, Profile, 1, false, CancellationToken.None));

        Assert.Equal("Tendons", report.Routine.Name);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], report.Routine.Days);
        Assert.Equal(["Chaise contre le mur", "Pont fessier"], report.Routine.Lines.Select(line => line.ExerciseName));
        Assert.True(report.PlannedToday);
        Assert.Equal((2, 2, 1), (report.DoneToday, report.TotalToday, report.PassesToday));
        Assert.True(report.CompletedToday);
        Assert.Equal(3, report.Streak);
        Assert.Equal(2, report.TodaySessions.Count);
        Assert.True(report.TodaySessions[0].Finished);
        Assert.Equal(now.AddHours(-9), report.TodaySessions[0].FinishedAt);
        var open = report.TodaySessions[1];
        Assert.False(open.Finished);
        Assert.Equal((0, 2), (open.ExercisesDone, open.ExercisesTotal));
        var ticked = Assert.Single(open.Completions);
        Assert.Equal((1, 60, 8, 1), (ticked.SetsDone, ticked.Value, ticked.Effort, ticked.Discomfort));
        Assert.Equal(5, report.Calendar.Count);
        Assert.Equal(["not_counted", "not_counted", "done", "done", "done"], report.Calendar.Select(day => day.Status));
        Assert.Equal(1, report.Calendar[^1].Passes);

        var progress = await contract.ExerciseProgressAsync(Household, Profile, wallSit.Id, 2, CancellationToken.None);
        Assert.Equal(60, progress.BestValue);
        Assert.Equal(12m, progress.BestLoadKg);
        Assert.Equal(3, progress.DaysDone);
        Assert.Equal(8, progress.Recent[0].Effort);
        Assert.Null((await contract.ExerciseProgressAsync(Household, Other, wallSit.Id, 2, CancellationToken.None)).BestValue);

        Assert.Equal((7, 2), ((await contract.SettingsAsync(Household, Profile, CancellationToken.None)) is var d ? (d.DefaultEffort, d.DefaultDiscomfort) : default));
        data.Settings = SportProfileSettings.Create(Household, Profile);
        data.Settings.Change(6, 0);
        Assert.Equal(6, (await contract.SettingsAsync(Household, Profile, CancellationToken.None)).DefaultEffort);
    }
}
