using LifeOS.Application.WeekPlanning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.WeekPlanning;

/// <summary>
/// EF Core reads backing the balanced plan engine: nutrition targets, activity energy and the
/// meal history around a week.
/// </summary>
public sealed class EfBalancedPlanDataSource : IBalancedPlanDataSource
{
    private readonly LifeOSDbContext _context;

    public EfBalancedPlanDataSource(LifeOSDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<NutritionTargets?> GetNutritionTargetsAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await _context.UserConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(config => config.HouseholdId == householdId, cancellationToken);

        if (configuration == null)
        {
            return null;
        }

        return new NutritionTargets(
            configuration.DailyBaseEnergyKcal,
            configuration.TargetNetDeficitKcal,
            configuration.TargetProteinG,
            configuration.TargetCarbsG,
            configuration.TargetFatsG);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetActivityEnergyByDayPlanAsync(
        Guid weekId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.ActivitySessions
            .AsNoTracking()
            .Where(session => _context.DayPlans.Any(day => day.Id == session.DayPlanId && day.WeekId == weekId))
            .GroupBy(session => session.DayPlanId)
            .Select(group => new { DayPlanId = group.Key, Energy = group.Sum(session => session.EstimatedEnergyKcal) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.DayPlanId, row => row.Energy);
    }

    public async Task<IReadOnlyList<PlannedMealReference>> GetPlannedMealReferencesAsync(
        Guid householdId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from week in _context.Weeks.AsNoTracking()
            join day in _context.DayPlans.AsNoTracking() on week.Id equals day.WeekId
            join meal in _context.PlannedMeals.AsNoTracking() on day.Id equals meal.DayPlanId
            where week.HouseholdId == householdId
                && day.Date >= fromDate
                && day.Date <= toDate
                && meal.Status != "skipped"
                && (meal.RecipeId != null || meal.ComposedMealId != null)
            select new { day.Date, meal.RecipeId, meal.ComposedMealId })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new PlannedMealReference(row.Date, row.RecipeId ?? row.ComposedMealId!.Value))
            .ToList();
    }
}
