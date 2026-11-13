using LifeOS.Domain.Sports;

namespace LifeOS.Application.Sports.Contracts;

public sealed record SportSessionDto(
    Guid Id, Guid MemberProfileId, DateOnly Date, string Sport, string SportLabel,
    Guid? TemplateId, string? TemplateName, int DurationMinutes, decimal? DistanceKm, int? Calories,
    int? Feeling, int? AverageHeartRate, int? MaxHeartRate, string Notes, string Source)
{
    public static SportSessionDto From(SportSession s, IReadOnlyDictionary<Guid, string> templateNames) => new(
        s.Id, s.MemberProfileId, s.Date, s.Sport, SportCatalog.Sports[s.Sport],
        s.TemplateId, s.TemplateId is { } id ? templateNames.GetValueOrDefault(id) : null,
        s.DurationMinutes, s.DistanceKm, s.Calories, s.Feeling, s.AverageHeartRate, s.MaxHeartRate, s.Notes, s.Source);
}

public sealed record WeeklyGoalDto(
    Guid Id, Guid MemberProfileId, string Kind, string KindLabel, Guid? TemplateId, string? TemplateName,
    string? Sport, string? Unit, decimal Target, string Label, DateOnly ValidFrom, DateOnly? ValidUntil)
{
    public static WeeklyGoalDto From(WeeklyGoal g, IReadOnlyDictionary<Guid, string> templateNames)
    {
        var templateName = g.TemplateId is { } id ? templateNames.GetValueOrDefault(id, "Séance type archivée") : null;
        var label = g.Kind switch
        {
            "template" => $"{templateName} ×{g.Target:0}",
            "sport" => $"{SportCatalog.Sports[g.Sport!]} ×{g.Target:0}",
            "sessions" => $"{g.Target:0} séances, tous sports",
            _ => g.Unit switch { "kcal" => $"{g.Target:0.##} kcal", "km" => $"{g.Target:0.##} km", _ => $"{g.Target:0.##} h" },
        };
        return new(g.Id, g.MemberProfileId, g.Kind, WeeklyGoal.Kinds[g.Kind], g.TemplateId, templateName,
            g.Sport, g.Unit, g.Target, label, g.ValidFrom, g.ValidUntil);
    }
}

public sealed record GoalProgressDto(WeeklyGoalDto Goal, decimal Achieved, bool Reached, bool Partial);

public sealed record WeekReportDto(
    DateOnly Monday, DateOnly Sunday, int SessionCount, int DurationMinutes, decimal DistanceKm,
    int Calories, int SessionsWithoutCalories, bool CaloriesComplete, int GoalsReached,
    IReadOnlyList<GoalProgressDto> Goals, IReadOnlyList<SportSessionDto> Sessions);

public sealed record DailyChallengeDto(
    Guid Id, Guid MemberProfileId, string Name, string Unit, int DailyTarget, int JokersPerWeek,
    DateOnly StartedOn, DateOnly? StoppedOn, bool IsActive)
{
    public static DailyChallengeDto From(DailyChallenge c, DateOnly today)
    {
        var rule = c.RuleOn(today);
        return new(c.Id, c.MemberProfileId, c.Name, c.Unit, rule.DailyTarget, rule.JokersPerWeek, c.StartedOn, c.StoppedOn, c.IsActive);
    }
}

public sealed record ChallengeEntryDto(Guid Id, DateOnly Date, int Quantity, DateTimeOffset LoggedAt)
{
    public static ChallengeEntryDto From(ChallengeEntry e) => new(e.Id, e.Date, e.Quantity, e.LoggedAt);
}

/// <param name="Status">not_counted, reached, joker, missed or in_progress.</param>
public sealed record ChallengeDayDto(DateOnly Date, int Total, int Target, int Percent, string Status);

public sealed record ChallengeReportDto(
    DailyChallengeDto Challenge, DateOnly Today, int TodayTotal, int TodayTarget, bool ReachedToday,
    int Streak, int Record, int JokersPerWeek, int JokersLeftThisWeek, int Total, int TotalThisYear,
    int? NextMilestone, IReadOnlyList<int> Milestones, IReadOnlyList<ChallengeEntryDto> TodayEntries,
    IReadOnlyList<ChallengeDayDto> Calendar);

/// <summary>Published read contract (ADR 0009): what one profile did in sport on one day.</summary>
public sealed record SportDayDto(DateOnly Date, Guid MemberProfileId, IReadOnlyList<SportDaySessionDto> Sessions,
    int Calories, bool CaloriesComplete);

public sealed record SportDaySessionDto(Guid Id, string Sport, int DurationMinutes, int? Calories);
