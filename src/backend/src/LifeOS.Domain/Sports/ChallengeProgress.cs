namespace LifeOS.Domain.Sports;

public enum ChallengeDayStatus
{
    /// <summary>Before the start or after the stop: not counted.</summary>
    NotCounted,
    Reached,
    /// <summary>Missed but covered by a joker of its week: the streak goes on.</summary>
    Joker,
    Missed,
    /// <summary>Today, not reached yet: it never breaks the streak before midnight.</summary>
    InProgress,
}

public sealed record ChallengeDay(DateOnly Date, int Total, int Target, ChallengeDayStatus Status)
{
    public int Percent => Target <= 0 ? 0 : Math.Min(100, Total * 100 / Target);
}

/// <summary>
/// Streak, record, calendar and totals of a challenge, computed on read (ADR 0013). A day is
/// reached when its entries reach that day's target. A missed past day uses a joker of its week
/// when one is left (the streak goes on without counting that day); otherwise the streak restarts.
/// </summary>
public sealed record ChallengeProgress(
    int TodayTotal,
    int TodayTarget,
    int Streak,
    int Record,
    int JokersPerWeek,
    int JokersLeftThisWeek,
    int Total,
    int TotalThisYear,
    int? NextMilestone,
    IReadOnlyList<int> Milestones,
    IReadOnlyList<ChallengeDay> Calendar)
{
    public static IReadOnlyList<int> MilestonesFor(string unit) => unit == "reps"
        ? [1_000, 5_000, 10_000, 25_000, 50_000, 100_000]
        : [600, 3_000, 6_000, 15_000, 30_000];

    /// <param name="dayTotals">Sum of the entries per day.</param>
    /// <param name="calendarFrom">First day returned in <see cref="Calendar"/>.</param>
    public static ChallengeProgress Compute(DailyChallenge challenge, IReadOnlyDictionary<DateOnly, int> dayTotals,
        DateOnly today, DateOnly calendarFrom)
    {
        var lastDay = challenge.StoppedOn is { } stopped && stopped < today ? stopped : today;
        var statuses = new Dictionary<DateOnly, ChallengeDayStatus>();
        var jokersUsed = new Dictionary<DateOnly, int>();
        int streak = 0, record = 0;
        for (var day = challenge.StartedOn; day <= lastDay; day = day.AddDays(1))
        {
            var rule = challenge.RuleOn(day);
            var reached = dayTotals.GetValueOrDefault(day) >= rule.DailyTarget;
            ChallengeDayStatus status;
            if (reached) { streak++; status = ChallengeDayStatus.Reached; }
            else if (day == today) status = ChallengeDayStatus.InProgress;
            else
            {
                var monday = SportWeek.Monday(day);
                var used = jokersUsed.GetValueOrDefault(monday);
                if (used < rule.JokersPerWeek) { jokersUsed[monday] = used + 1; status = ChallengeDayStatus.Joker; }
                else { streak = 0; status = ChallengeDayStatus.Missed; }
            }
            record = Math.Max(record, streak);
            statuses[day] = status;
        }

        var todayRule = challenge.RuleOn(today);
        var calendar = new List<ChallengeDay>();
        for (var day = calendarFrom; day <= today; day = day.AddDays(1))
            calendar.Add(new ChallengeDay(day, dayTotals.GetValueOrDefault(day), challenge.RuleOn(day).DailyTarget,
                statuses.GetValueOrDefault(day, ChallengeDayStatus.NotCounted)));

        var total = dayTotals.Values.Sum();
        var milestones = MilestonesFor(challenge.Unit);
        return new ChallengeProgress(
            TodayTotal: dayTotals.GetValueOrDefault(today),
            TodayTarget: todayRule.DailyTarget,
            Streak: challenge.IsActive || challenge.StoppedOn >= today ? streak : 0,
            Record: record,
            JokersPerWeek: todayRule.JokersPerWeek,
            JokersLeftThisWeek: Math.Max(0, todayRule.JokersPerWeek - jokersUsed.GetValueOrDefault(SportWeek.Monday(today))),
            Total: total,
            TotalThisYear: dayTotals.Where(entry => entry.Key.Year == today.Year).Sum(entry => entry.Value),
            NextMilestone: milestones.Cast<int?>().FirstOrDefault(milestone => milestone > total),
            Milestones: milestones,
            Calendar: calendar);
    }
}
