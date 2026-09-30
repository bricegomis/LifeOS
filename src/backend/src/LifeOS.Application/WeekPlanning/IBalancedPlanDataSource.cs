namespace LifeOS.Application.WeekPlanning;

/// <summary>
/// Daily nutrition targets for the household, as configured by the user.
/// </summary>
public sealed record NutritionTargets(
    decimal DailyBaseEnergyKcal,
    decimal TargetNetDeficitKcal,
    decimal TargetProteinG,
    decimal TargetCarbsG,
    decimal TargetFatsG);

/// <summary>
/// A meal reference (recipe or composed meal) planned on a given date, used to measure
/// diversity over the surrounding month.
/// </summary>
public sealed record PlannedMealReference(DateOnly Date, Guid ReferenceId);

/// <summary>
/// Read-only data the balanced plan engine needs and that no existing repository exposes:
/// nutrition targets, activity energy per day and the meal history around the week.
/// </summary>
public interface IBalancedPlanDataSource
{
    /// <summary>
    /// Returns the household nutrition targets, or <c>null</c> when the user never configured them.
    /// </summary>
    Task<NutritionTargets?> GetNutritionTargetsAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total estimated activity energy (kcal) per day plan of a week.
    /// Day plans without any activity session are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetActivityEnergyByDayPlanAsync(
        Guid weekId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every meal reference planned by the household between two dates (inclusive),
    /// used to measure how repetitive the week is within its month.
    /// </summary>
    Task<IReadOnlyList<PlannedMealReference>> GetPlannedMealReferencesAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
