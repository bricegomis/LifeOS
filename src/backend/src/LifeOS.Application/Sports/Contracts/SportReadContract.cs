using LifeOS.Domain.Sports;

namespace LifeOS.Application.Sports.Contracts;

/// <summary>Reads the Sport module needs; implemented by Infrastructure on the sports tables.</summary>
public interface ISportReadData
{
    /// <summary>Sessions dated from <paramref name="from"/> to <paramref name="to"/> included; all profiles when null.</summary>
    Task<IReadOnlyList<SportSession>> SessionsAsync(Guid householdId, Guid? profileId, DateOnly from, DateOnly to, CancellationToken ct);
    /// <summary>Goals valid on at least one Monday from <paramref name="fromMonday"/> to <paramref name="toMonday"/>.</summary>
    Task<IReadOnlyList<WeeklyGoal>> GoalsAsync(Guid householdId, Guid profileId, DateOnly fromMonday, DateOnly toMonday, CancellationToken ct);
    /// <summary>Names of every template of the household, archived ones included.</summary>
    Task<IReadOnlyDictionary<Guid, string>> TemplateNamesAsync(Guid householdId, CancellationToken ct);
    Task<IReadOnlyList<DailyChallenge>> ChallengesAsync(Guid householdId, Guid profileId, bool activeOnly, CancellationToken ct);
    /// <summary>Sum of the entries per challenge and per day.</summary>
    Task<IReadOnlyDictionary<Guid, Dictionary<DateOnly, int>>> DayTotalsAsync(Guid householdId, IReadOnlyCollection<Guid> challengeIds, CancellationToken ct);
    Task<IReadOnlyList<ChallengeEntry>> EntriesAsync(Guid householdId, IReadOnlyCollection<Guid> challengeIds, DateOnly day, CancellationToken ct);
}

/// <summary>
/// Published read contract of the Sport module (ADR 0009, ADR 0013): week reports, challenge
/// reports and sport days, all computed on read. The screens, the assistant and the future
/// planner read the module through it.
/// </summary>
public sealed class SportReadContract(ISportReadData data, TimeProvider clock)
{
    public DateOnly Today() => SportClock.Today(clock);

    /// <summary>The <paramref name="count"/> weeks ending with the week of <paramref name="lastDay"/>, oldest first.</summary>
    public async Task<IReadOnlyList<WeekReportDto>> WeeksAsync(Guid householdId, Guid profileId, DateOnly lastDay, int count,
        CancellationToken ct)
    {
        if (count is < 1 or > 104) throw new ArgumentException("Nombre de semaines : 1 à 104.");
        var lastMonday = SportWeek.Monday(lastDay);
        var firstMonday = lastMonday.AddDays(-7 * (count - 1));
        var sessions = await data.SessionsAsync(householdId, profileId, firstMonday, lastMonday.AddDays(6), ct);
        var goals = await data.GoalsAsync(householdId, profileId, firstMonday, lastMonday, ct);
        var names = await data.TemplateNamesAsync(householdId, ct);
        return Enumerable.Range(0, count).Select(i => Report(WeekSummary.Compute(firstMonday.AddDays(7 * i), sessions, goals), names)).ToList();
    }

    /// <summary>Running challenges (or all of them) with streak, totals and a calendar of <paramref name="calendarWeeks"/> weeks.</summary>
    public async Task<IReadOnlyList<ChallengeReportDto>> ChallengesAsync(Guid householdId, Guid profileId, int calendarWeeks,
        bool includeStopped, CancellationToken ct)
    {
        if (calendarWeeks is < 0 or > 53) throw new ArgumentException("Calendrier : 0 à 53 semaines.");
        var today = Today();
        var challenges = await data.ChallengesAsync(householdId, profileId, !includeStopped, ct);
        var ids = challenges.Select(c => c.Id).ToList();
        var totals = await data.DayTotalsAsync(householdId, ids, ct);
        var entries = await data.EntriesAsync(householdId, ids, today, ct);
        var calendarFrom = calendarWeeks == 0 ? today : SportWeek.Monday(today).AddDays(-7 * (calendarWeeks - 1));
        return challenges.Select(challenge => Report(challenge,
            ChallengeProgress.Compute(challenge, totals.GetValueOrDefault(challenge.Id) ?? [], today, calendarFrom),
            entries.Where(e => e.ChallengeId == challenge.Id).OrderBy(e => e.LoggedAt).ToList(), today)).ToList();
    }

    /// <summary>Sessions per day and per profile, for the future planner.</summary>
    public async Task<IReadOnlyList<SportDayDto>> DaysAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > 366) throw new ArgumentException("Période : 1 à 366 jours.");
        var sessions = await data.SessionsAsync(householdId, null, from, to, ct);
        return sessions.GroupBy(s => (s.Date, s.MemberProfileId)).OrderBy(g => g.Key.Date)
            .Select(g => new SportDayDto(g.Key.Date, g.Key.MemberProfileId,
                g.Select(s => new SportDaySessionDto(s.Id, s.Sport, s.DurationMinutes, s.Calories)).ToList(),
                g.Sum(s => s.Calories ?? 0), g.All(s => s.Calories is not null)))
            .ToList();
    }

    private static WeekReportDto Report(WeekSummary week, IReadOnlyDictionary<Guid, string> names) => new(
        week.Monday, week.Monday.AddDays(6), week.Sessions.Count, week.DurationMinutes, week.DistanceKm,
        week.Calories, week.SessionsWithoutCalories, week.CaloriesComplete, week.GoalsReached,
        week.Goals.Select(g => new GoalProgressDto(WeeklyGoalDto.From(g.Goal, names), g.Achieved, g.Reached, g.Partial)).ToList(),
        week.Sessions.OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt).Select(s => SportSessionDto.From(s, names)).ToList());

    private static ChallengeReportDto Report(DailyChallenge challenge, ChallengeProgress p, IReadOnlyList<ChallengeEntry> todayEntries,
        DateOnly today) => new(
        DailyChallengeDto.From(challenge, today), today, p.TodayTotal, p.TodayTarget, p.TodayTotal >= p.TodayTarget,
        p.Streak, p.Record, p.JokersPerWeek, p.JokersLeftThisWeek, p.Total, p.TotalThisYear, p.NextMilestone, p.Milestones,
        todayEntries.Select(ChallengeEntryDto.From).ToList(),
        p.Calendar.Select(d => new ChallengeDayDto(d.Date, d.Total, d.Target, d.Percent, Status(d.Status))).ToList());

    private static string Status(ChallengeDayStatus status) => status switch
    {
        ChallengeDayStatus.Reached => "reached",
        ChallengeDayStatus.Joker => "joker",
        ChallengeDayStatus.Missed => "missed",
        ChallengeDayStatus.InProgress => "in_progress",
        _ => "not_counted",
    };
}
