using LifeOS.Application.WeekPlanning;
using LifeOS.Domain.WeekPlanning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.WeekPlanning;

public sealed class EfWeekRepository(LifeOSDbContext dbContext) : IWeekRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<Week?> GetByIdAsync(Guid weekId, Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Weeks
            .AsNoTracking()
            .Include(w => w.DayPlans)
            .FirstOrDefaultAsync(w => w.Id == weekId && w.HouseholdId == householdId, cancellationToken);
    }

    public async Task<IReadOnlyList<Week>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Weeks
            .AsNoTracking()
            .Where(w => w.HouseholdId == householdId)
            .Include(w => w.DayPlans)
            .OrderByDescending(w => w.StartsOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<Week?> GetByStartDateAsync(Guid householdId, DateOnly startsOn, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Weeks
            .AsNoTracking()
            .Include(w => w.DayPlans)
            .FirstOrDefaultAsync(w => w.HouseholdId == householdId && w.StartsOn == startsOn, cancellationToken);
    }

    public async Task<Week> AddAsync(Week week, CancellationToken cancellationToken = default)
    {
        await _dbContext.Weeks.AddAsync(week, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return week;
    }

    public async Task<Week> UpdateAsync(Week week, CancellationToken cancellationToken = default)
    {
        _dbContext.Weeks.Update(week);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return week;
    }

    public async Task<bool> DeleteAsync(Guid weekId, Guid householdId, CancellationToken cancellationToken = default)
    {
        var week = await _dbContext.Weeks
            .FirstOrDefaultAsync(w => w.Id == weekId && w.HouseholdId == householdId, cancellationToken);

        if (week == null)
        {
            return false;
        }

        _dbContext.Weeks.Remove(week);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class EfDayPlanRepository(LifeOSDbContext dbContext) : IDayPlanRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<DayPlan?> GetByIdAsync(Guid dayPlanId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DayPlans
            .AsNoTracking()
            .Include(d => d.PlannedMeals)
            .FirstOrDefaultAsync(d => d.Id == dayPlanId, cancellationToken);
    }

    public async Task<IReadOnlyList<DayPlan>> GetAllForWeekAsync(Guid weekId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DayPlans
            .AsNoTracking()
            .Where(d => d.WeekId == weekId)
            .Include(d => d.PlannedMeals)
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<DayPlan> AddAsync(DayPlan dayPlan, CancellationToken cancellationToken = default)
    {
        await _dbContext.DayPlans.AddAsync(dayPlan, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return dayPlan;
    }

    public async Task<DayPlan> UpdateAsync(DayPlan dayPlan, CancellationToken cancellationToken = default)
    {
        _dbContext.DayPlans.Update(dayPlan);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return dayPlan;
    }

    public async Task<bool> DeleteAsync(Guid dayPlanId, CancellationToken cancellationToken = default)
    {
        var dayPlan = await _dbContext.DayPlans
            .FirstOrDefaultAsync(d => d.Id == dayPlanId, cancellationToken);

        if (dayPlan == null)
        {
            return false;
        }

        _dbContext.DayPlans.Remove(dayPlan);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class EfPlannedMealRepository(LifeOSDbContext dbContext) : IPlannedMealRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<PlannedMeal?> GetByIdAsync(Guid mealId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlannedMeals
            .AsNoTracking()
            .Include(m => m.Parts)
            .FirstOrDefaultAsync(m => m.Id == mealId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlannedMeal>> GetAllForDayAsync(Guid dayPlanId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlannedMeals
            .AsNoTracking()
            .Where(m => m.DayPlanId == dayPlanId)
            .Include(m => m.Parts)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlannedMeal> AddAsync(PlannedMeal meal, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlannedMeals.AddAsync(meal, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return meal;
    }

    public async Task<PlannedMeal> UpdateAsync(PlannedMeal meal, CancellationToken cancellationToken = default)
    {
        _dbContext.PlannedMeals.Update(meal);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return meal;
    }

    public async Task<bool> DeleteAsync(Guid mealId, CancellationToken cancellationToken = default)
    {
        var meal = await _dbContext.PlannedMeals
            .FirstOrDefaultAsync(m => m.Id == mealId, cancellationToken);

        if (meal == null)
        {
            return false;
        }

        _dbContext.PlannedMeals.Remove(meal);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
