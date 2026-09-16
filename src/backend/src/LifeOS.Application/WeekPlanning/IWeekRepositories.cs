using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Application.WeekPlanning;

public interface IWeekRepository
{
    Task<Week?> GetByIdAsync(Guid weekId, Guid householdId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Week>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<Week?> GetByStartDateAsync(Guid householdId, DateOnly startsOn, CancellationToken cancellationToken = default);
    Task<Week> AddAsync(Week week, CancellationToken cancellationToken = default);
    Task<Week> UpdateAsync(Week week, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid weekId, Guid householdId, CancellationToken cancellationToken = default);
}

public interface IDayPlanRepository
{
    Task<DayPlan?> GetByIdAsync(Guid dayPlanId, Guid householdId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DayPlan>> GetAllForWeekAsync(Guid weekId, Guid householdId, CancellationToken cancellationToken = default);
    Task<DayPlan> AddAsync(DayPlan dayPlan, CancellationToken cancellationToken = default);
    Task<DayPlan> UpdateAsync(DayPlan dayPlan, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid dayPlanId, Guid householdId, CancellationToken cancellationToken = default);
}

public interface IPlannedMealRepository
{
    Task<PlannedMeal?> GetByIdAsync(Guid mealId, Guid householdId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlannedMeal>> GetAllForDayAsync(Guid dayPlanId, Guid householdId, CancellationToken cancellationToken = default);
    Task<PlannedMeal> AddAsync(PlannedMeal meal, CancellationToken cancellationToken = default);
    Task<PlannedMeal> UpdateAsync(PlannedMeal meal, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid mealId, Guid householdId, CancellationToken cancellationToken = default);
}
