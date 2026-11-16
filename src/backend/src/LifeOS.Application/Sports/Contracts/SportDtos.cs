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

/// <param name="Measure">hold (seconds) or reps.</param>
/// <param name="DefaultLoadKg">Usual load offered as the target in a routine; null means bodyweight.</param>
/// <param name="MeasureLocked">The exercise has been done: its measure no longer changes.</param>
public sealed record ExerciseDto(Guid Id, string Name, string? Instructions, string Measure, decimal? DefaultLoadKg, bool HasImage,
    bool IsArchived, bool MeasureLocked)
{
    public static ExerciseDto From(Exercise e, bool measureLocked) =>
        new(e.Id, e.Name, e.Instructions, e.Measure, e.DefaultLoadKg, e.HasImage, e.IsArchived, measureLocked);
}

/// <param name="Target">Seconds or repetitions per set.</param>
/// <param name="LoadKg">Null means bodyweight.</param>
public sealed record RoutineLineDto(Guid ExerciseId, string ExerciseName, string? Instructions, string Measure,
    bool HasImage, int Sets, int Target, decimal? LoadKg);

/// <param name="Days">Days of the week, 1 (Monday) to 7 (Sunday).</param>
public sealed record RoutineDto(Guid Id, Guid MemberProfileId, string Name, IReadOnlyList<int> Days, int JokersPerWeek,
    IReadOnlyList<RoutineLineDto> Lines, DateOnly StartedOn, DateOnly? StoppedOn, bool IsActive)
{
    public static RoutineDto From(Routine r, DateOnly today, IReadOnlyDictionary<Guid, Exercise> exercises)
    {
        var version = r.VersionOn(today);
        return new(r.Id, r.MemberProfileId, r.Name,
            Enumerable.Range(1, 7).Where(day => (version.Days & (1 << (day - 1))) != 0).ToList(), version.JokersPerWeek,
            version.OrderedLines.Select(line =>
            {
                var e = exercises[line.ExerciseId];
                return new RoutineLineDto(e.Id, e.Name, e.Instructions, e.Measure, e.HasImage, line.Sets, line.Target, line.LoadKg);
            }).ToList(),
            r.StartedOn, r.StoppedOn, r.IsActive);
    }
}

/// <param name="SetsDone">Sets ticked, each at <paramref name="Value"/> and <paramref name="LoadKg"/>.</param>
/// <param name="Effort">Perceived effort 1 to 10; null when not noted.</param>
/// <param name="Discomfort">Discomfort 0 (none) to 10; null when not noted.</param>
public sealed record ExerciseCompletionDto(Guid SessionId, Guid ExerciseId, DateOnly Date, int SetsDone, int Value, decimal? LoadKg,
    int? Effort, int? Discomfort, DateTimeOffset LoggedAt)
{
    public static ExerciseCompletionDto From(ExerciseCompletion c) =>
        new(c.SessionId, c.ExerciseId, c.Date, c.SetsDone, c.Value, c.LoadKg, c.Effort, c.Discomfort, c.LoggedAt);
}

/// <summary>One pass of a routine (ADR 0015); finished when every exercise has all its sets.</summary>
/// <param name="FinishedAt">Time of the last tick once finished; null while in progress.</param>
public sealed record RoutineSessionDto(Guid Id, DateOnly Date, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, bool Finished,
    int ExercisesDone, int ExercisesTotal, string? Comment, IReadOnlyList<ExerciseCompletionDto> Completions);

/// <param name="Done">Exercises done in the best pass of the day.</param>
/// <param name="Passes">Passes finished that day.</param>
/// <param name="Status">not_counted, not_planned, done, joker, missed or in_progress.</param>
public sealed record RoutineDayDto(DateOnly Date, int Done, int Planned, int Percent, string Status, int Passes);

/// <param name="DoneToday">Exercises done in today's best pass.</param>
/// <param name="CompletedToday">At least one pass finished today.</param>
/// <param name="PassesToday">Passes finished today.</param>
/// <param name="TodaySessions">Today's passes, oldest first; at most one in progress.</param>
/// <param name="NextPlannedDay">Next day of the routine after today; null once stopped.</param>
public sealed record RoutineReportDto(
    RoutineDto Routine, DateOnly Today, bool PlannedToday, int DoneToday, int TotalToday, bool CompletedToday, int PassesToday,
    DateOnly? NextPlannedDay, int Streak, int Record, int JokersPerWeek, int JokersLeftThisWeek,
    IReadOnlyList<RoutineSessionDto> TodaySessions, IReadOnlyList<RoutineDayDto> Calendar);

public sealed record ExerciseWeekDto(DateOnly Monday, int? BestValue, decimal? BestLoadKg);

/// <summary>Follow-up of one exercise for a profile: records, the best of each week and the latest ticks with their feeling.</summary>
public sealed record ExerciseProgressDto(Guid ExerciseId, int? BestValue, decimal? BestLoadKg, int DaysDone,
    IReadOnlyList<ExerciseWeekDto> Weeks, IReadOnlyList<ExerciseCompletionDto> Recent);

/// <summary>Effort and discomfort offered when a pass ends (ADR 0015).</summary>
public sealed record SportSettingsDto(Guid MemberProfileId, int DefaultEffort, int DefaultDiscomfort);

/// <summary>Published read contract (ADR 0009): what one profile did in sport on one day.</summary>
public sealed record SportDayDto(DateOnly Date, Guid MemberProfileId, IReadOnlyList<SportDaySessionDto> Sessions,
    int Calories, bool CaloriesComplete);

public sealed record SportDaySessionDto(Guid Id, string Sport, int DurationMinutes, int? Calories);
