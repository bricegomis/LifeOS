using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// A day in a week plan.
/// </summary>
public sealed class DayPlan : Entity
{
    public Guid WeekId { get; private set; }
    public DateOnly Date { get; private set; }
    public string WorkContext { get; private set; } // home, office, off
    public bool BikeCommute { get; private set; }

    // Navigation property for EF Core
    private readonly List<PlannedMeal> _plannedMeals = [];
    public IReadOnlyList<PlannedMeal> PlannedMeals => _plannedMeals.AsReadOnly();

    private DayPlan(
        Guid id,
        Guid weekId,
        DateOnly date,
        string workContext,
        bool bikeCommute)
        : base(id)
    {
        WeekId = weekId;
        Date = date;
        WorkContext = workContext;
        BikeCommute = bikeCommute;
    }

    public static DayPlan Create(
        Guid weekId,
        DateOnly date,
        string workContext = "home",
        bool bikeCommute = false)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        if (string.IsNullOrWhiteSpace(workContext))
        {
            throw new ArgumentException("Work context is required.", nameof(workContext));
        }

        return new DayPlan(
            Guid.NewGuid(),
            weekId,
            date,
            workContext.ToLowerInvariant(),
            bikeCommute);
    }

    /// <summary>
    /// Updates day plan context.
    /// </summary>
    public void Update(string? workContext = null, bool? bikeCommute = null)
    {
        if (!string.IsNullOrWhiteSpace(workContext))
        {
            WorkContext = workContext.ToLowerInvariant();
        }

        if (bikeCommute.HasValue)
        {
            BikeCommute = bikeCommute.Value;
        }
    }

    /// <summary>
    /// Rehydrates a <see cref="DayPlan"/> from persisted state.
    /// </summary>
    public static DayPlan Rehydrate(
        Guid id,
        Guid weekId,
        DateOnly date,
        string workContext,
        bool bikeCommute)
    {
        return new DayPlan(id, weekId, date, workContext, bikeCommute);
    }

    /// <summary>
    /// Called by EF Core to load planned meals.
    /// </summary>
    internal void SetPlannedMeals(List<PlannedMeal> plannedMeals)
    {
        _plannedMeals.Clear();
        _plannedMeals.AddRange(plannedMeals);
    }
}
