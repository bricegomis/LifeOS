using LifeOS.Domain.Sports;

namespace LifeOS.Domain.Tests.Sports;

public sealed class WeeklyGoalTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateOnly Friday = new(2026, 11, 13);
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static SportSession Session(DateOnly date, string sport, Guid? template = null, int? calories = 300, decimal? km = null, int minutes = 45) =>
        SportSession.Create(Household, Profile, new SportSessionValues(date, sport, template, minutes, km, calories, null, null, null, null), Friday, Now);

    [Fact]
    public void Weeks_start_on_monday()
    {
        Assert.Equal(Monday, SportWeek.Monday(Friday));
        Assert.Equal(Monday, SportWeek.Monday(Monday));
        Assert.Equal(Monday, SportWeek.Monday(new DateOnly(2026, 11, 15)));
    }

    [Fact]
    public void A_goal_starts_on_the_current_monday_and_checks_its_shape()
    {
        var goal = WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sport", null, "running", null, 2), Friday);
        Assert.Equal(Monday, goal.ValidFrom);
        Assert.Throws<ArgumentException>(() => WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("template", null, null, null, 2), Friday));
        Assert.Throws<ArgumentException>(() => WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sessions", null, null, null, 2.5m), Friday));
        Assert.Throws<ArgumentException>(() => WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("volume", null, null, "steps", 10), Friday));
    }

    [Fact]
    public void Changing_an_older_goal_keeps_it_for_past_weeks_and_starts_a_new_one_this_week()
    {
        var goal = WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sessions", null, null, null, 3), Friday.AddDays(-14));
        var replacement = goal.ChangeFrom(Friday, new WeeklyGoalValues("sessions", null, null, null, 4));
        Assert.NotNull(replacement);
        Assert.Equal(Monday, goal.ValidUntil);
        Assert.True(goal.AppliesTo(Monday.AddDays(-7)));
        Assert.False(goal.AppliesTo(Monday));
        Assert.True(replacement.AppliesTo(Monday));
        Assert.Equal(4, replacement.Target);
    }

    [Fact]
    public void Changing_a_goal_created_this_week_edits_it_in_place()
    {
        var goal = WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sessions", null, null, null, 3), Friday);
        Assert.Null(goal.ChangeFrom(Friday, new WeeklyGoalValues("sessions", null, null, null, 4)));
        Assert.Equal(4, goal.Target);
    }

    [Fact]
    public void A_stopped_goal_still_counts_this_week_and_a_goal_of_this_week_is_deleted()
    {
        var old = WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sessions", null, null, null, 3), Friday.AddDays(-7));
        Assert.True(old.Stop(Friday));
        Assert.True(old.AppliesTo(Monday));
        Assert.False(old.AppliesTo(Monday.AddDays(7)));
        var fresh = WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sessions", null, null, null, 3), Friday);
        Assert.False(fresh.Stop(Friday));
    }

    [Fact]
    public void The_week_summary_counts_each_goal_shape_and_marks_unknown_calories_as_partial()
    {
        var footing = Guid.NewGuid();
        var sessions = new[]
        {
            Session(Monday.AddDays(1), "running", footing, 520, 8.2m, 46),
            Session(Monday.AddDays(2), "strength", null, 380, null, 55),
            Session(Monday.AddDays(3), "crossfit", null, null, null, 60),
            Session(Monday.AddDays(-1), "running", footing, 500, 8, 44),
        };
        var goals = new[]
        {
            WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("template", footing, null, null, 2), Friday),
            WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sport", null, "strength", null, 1), Friday),
            WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("sessions", null, null, null, 4), Friday),
            WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("volume", null, null, "kcal", 1500), Friday),
            WeeklyGoal.Create(Household, Profile, new WeeklyGoalValues("volume", null, null, "hours", 2.5m), Friday),
        };

        var week = WeekSummary.Compute(Monday, sessions, goals);

        Assert.Equal(3, week.Sessions.Count);
        Assert.Equal(900, week.Calories);
        Assert.False(week.CaloriesComplete);
        Assert.Equal(8.2m, week.DistanceKm);
        Assert.Equal(161, week.DurationMinutes);
        Assert.Equal([1m, 1m, 3m, 900m, 2.68m], week.Goals.Select(g => g.Achieved));
        Assert.Equal([false, true, false, false, true], week.Goals.Select(g => g.Reached));
        Assert.True(week.Goals[3].Partial);
        Assert.Equal(2, week.GoalsReached);
    }
}
