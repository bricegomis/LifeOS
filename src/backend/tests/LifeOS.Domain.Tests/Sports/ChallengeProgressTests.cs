using LifeOS.Domain.Sports;

namespace LifeOS.Domain.Tests.Sports;

public sealed class ChallengeProgressTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateOnly Friday = new(2026, 11, 13);
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static DailyChallenge Pushups(DateOnly start, int jokers = 0) =>
        DailyChallenge.Create(Household, Profile, "Pompes", "reps", 100, jokers, start);

    private static Dictionary<DateOnly, int> Days(DateOnly from, params int[] totals) =>
        totals.Select((total, i) => (from.AddDays(i), total)).Where(d => d.total > 0).ToDictionary(d => d.Item1, d => d.total);

    [Fact]
    public void Each_reached_day_extends_the_streak_and_today_never_breaks_it_before_midnight()
    {
        var start = Friday.AddDays(-4);
        var progress = ChallengeProgress.Compute(Pushups(start), Days(start, 100, 120, 100, 100, 45), Friday, start);
        Assert.Equal(4, progress.Streak);
        Assert.Equal(4, progress.Record);
        Assert.Equal(45, progress.TodayTotal);
        Assert.Equal(ChallengeDayStatus.InProgress, progress.Calendar[^1].Status);
        Assert.Equal(45, progress.Calendar[^1].Percent);
    }

    [Fact]
    public void A_missed_day_restarts_the_streak_but_keeps_the_record()
    {
        var start = Friday.AddDays(-5);
        var progress = ChallengeProgress.Compute(Pushups(start), Days(start, 100, 100, 100, 30, 100, 100), Friday, start);
        Assert.Equal(2, progress.Streak);
        Assert.Equal(3, progress.Record);
        Assert.Equal(ChallengeDayStatus.Missed, progress.Calendar[3].Status);
    }

    [Fact]
    public void A_joker_covers_a_missed_day_of_its_week_without_counting_it()
    {
        // Monday 9 to Friday 13: Wednesday missed, one joker per week.
        var monday = new DateOnly(2026, 11, 9);
        var progress = ChallengeProgress.Compute(Pushups(monday, jokers: 1), Days(monday, 100, 100, 0, 100, 100), Friday, monday);
        Assert.Equal(4, progress.Streak);
        Assert.Equal(ChallengeDayStatus.Joker, progress.Calendar[2].Status);
        Assert.Equal(0, progress.JokersLeftThisWeek);
    }

    [Fact]
    public void Jokers_are_counted_per_week()
    {
        // Thursday 5 and Friday 6 missed: one joker that week, so Friday 6 breaks the streak.
        var start = new DateOnly(2026, 11, 2);
        var progress = ChallengeProgress.Compute(Pushups(start, jokers: 1),
            Days(start, 100, 100, 100, 0, 0, 100, 100, 100, 100, 100, 100, 100), Friday, start);
        Assert.Equal(ChallengeDayStatus.Joker, progress.Calendar[3].Status);
        Assert.Equal(ChallengeDayStatus.Missed, progress.Calendar[4].Status);
        Assert.Equal(7, progress.Streak);
        Assert.Equal(1, progress.JokersLeftThisWeek);
    }

    [Fact]
    public void A_new_target_applies_from_its_day_only()
    {
        var start = Friday.AddDays(-2);
        var challenge = Pushups(start);
        challenge.ChangeRule(50, 0, Friday.AddDays(-1));
        var progress = ChallengeProgress.Compute(challenge, Days(start, 100, 60, 50), Friday, start);
        Assert.Equal(3, progress.Streak);
        Assert.Equal(100, progress.Calendar[0].Target);
        Assert.Equal(50, progress.TodayTarget);
    }

    [Fact]
    public void Totals_and_next_milestone_count_every_entry()
    {
        var start = new DateOnly(2025, 12, 30);
        var totals = new Dictionary<DateOnly, int> { [start] = 4_900, [Friday] = 150 };
        var progress = ChallengeProgress.Compute(Pushups(start), totals, Friday, Friday);
        Assert.Equal(5_050, progress.Total);
        Assert.Equal(150, progress.TotalThisYear);
        Assert.Equal(10_000, progress.NextMilestone);
        Assert.Single(progress.Calendar);
    }

    [Fact]
    public void A_challenge_takes_sets_from_its_start_to_today_only()
    {
        var challenge = Pushups(Friday.AddDays(-1));
        Assert.Equal(20, challenge.Log(Friday, 20, Friday, Now).Quantity);
        Assert.Throws<ArgumentException>(() => challenge.Log(Friday.AddDays(1), 20, Friday, Now));
        Assert.Throws<ArgumentException>(() => challenge.Log(Friday.AddDays(-2), 20, Friday, Now));
        Assert.Throws<ArgumentException>(() => challenge.Log(Friday, 0, Friday, Now));
        challenge.Stop(Friday);
        Assert.Throws<ArgumentException>(() => challenge.Log(Friday, 10, Friday, Now));
    }

    [Fact]
    public void A_challenge_checks_its_unit_target_and_jokers()
    {
        Assert.Throws<ArgumentException>(() => DailyChallenge.Create(Household, Profile, "Pompes", "sets", 100, 0, Friday));
        Assert.Throws<ArgumentException>(() => DailyChallenge.Create(Household, Profile, "Pompes", "reps", 0, 0, Friday));
        Assert.Throws<ArgumentException>(() => DailyChallenge.Create(Household, Profile, "Pompes", "reps", 100, 3, Friday));
        Assert.Throws<ArgumentException>(() => DailyChallenge.Create(Household, Profile, " ", "reps", 100, 0, Friday));
    }
}
