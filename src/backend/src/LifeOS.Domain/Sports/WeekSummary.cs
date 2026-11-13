namespace LifeOS.Domain.Sports;

public sealed record GoalProgress(WeeklyGoal Goal, decimal Achieved, bool Reached, bool Partial);

/// <summary>
/// One week of one profile, computed on read (ADR 0013). Unknown calories stay unknown: the
/// total is then partial, never completed with zeros presented as known.
/// </summary>
public sealed record WeekSummary(
    DateOnly Monday,
    IReadOnlyList<SportSession> Sessions,
    int DurationMinutes,
    decimal DistanceKm,
    int Calories,
    int SessionsWithoutCalories,
    IReadOnlyList<GoalProgress> Goals)
{
    public bool CaloriesComplete => SessionsWithoutCalories == 0;
    public int GoalsReached => Goals.Count(goal => goal.Reached);

    /// <param name="sessions">Sessions of the profile; those outside the week are ignored.</param>
    /// <param name="goals">Goals of the profile; those not valid this week are ignored.</param>
    public static WeekSummary Compute(DateOnly monday, IEnumerable<SportSession> sessions, IEnumerable<WeeklyGoal> goals)
    {
        if (SportWeek.Monday(monday) != monday) throw new ArgumentException("Une semaine commence un lundi.");
        var sunday = monday.AddDays(6);
        var week = sessions.Where(s => s.Date >= monday && s.Date <= sunday)
            .OrderBy(s => s.Date).ThenBy(s => s.CreatedAt).ToList();
        var calories = week.Sum(s => s.Calories ?? 0);
        var missing = week.Count(s => s.Calories is null);
        var progress = goals.Where(goal => goal.AppliesTo(monday)).OrderBy(goal => goal.ValidFrom)
            .Select(goal => Progress(goal, week)).ToList();
        return new WeekSummary(monday, week, week.Sum(s => s.DurationMinutes), week.Sum(s => s.DistanceKm ?? 0),
            calories, missing, progress);
    }

    private static GoalProgress Progress(WeeklyGoal goal, IReadOnlyList<SportSession> week)
    {
        var (achieved, partial) = goal.Kind switch
        {
            "template" => ((decimal)week.Count(s => s.TemplateId == goal.TemplateId), false),
            "sport" => (week.Count(s => s.Sport == goal.Sport), false),
            "sessions" => (week.Count, false),
            _ => goal.Unit switch
            {
                "kcal" => ((decimal)week.Sum(s => s.Calories ?? 0), week.Any(s => s.Calories is null)),
                "km" => (week.Sum(s => s.DistanceKm ?? 0), false),
                _ => (Math.Round(week.Sum(s => s.DurationMinutes) / 60m, 2), false),
            },
        };
        return new GoalProgress(goal, achieved, achieved >= goal.Target, partial);
    }
}
