using LifeOS.Application.WeekPlanning;
using LifeOS.Domain.WeekPlanning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.WeekPlanning;

/// <summary>
/// EF Core implementation of the balanced week plan repository.
/// </summary>
public sealed class EfBalancedWeekPlanRepository : IBalancedWeekPlanRepository
{
    private readonly LifeOSDbContext _context;

    public EfBalancedWeekPlanRepository(LifeOSDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<BalancedWeekPlan?> GetByIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("Plan ID is required.", nameof(planId));
        }

        return await _context.BalancedWeekPlans
            .FirstOrDefaultAsync(plan => plan.Id == planId, cancellationToken);
    }

    public async Task<IReadOnlyList<BalancedWeekPlan>> GetAllForWeekAsync(Guid weekId, CancellationToken cancellationToken = default)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        return await _context.BalancedWeekPlans
            .Where(plan => plan.WeekId == weekId)
            .ToListAsync(cancellationToken);
    }

    public Task<BalancedWeekPlan> AddAsync(BalancedWeekPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        _context.BalancedWeekPlans.Add(plan);
        return Task.FromResult(plan);
    }

    public Task<BalancedWeekPlan> UpdateAsync(BalancedWeekPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        _context.BalancedWeekPlans.Update(plan);
        return Task.FromResult(plan);
    }

    public async Task<bool> DeleteAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("Plan ID is required.", nameof(planId));
        }

        var plan = await GetByIdAsync(planId, cancellationToken);
        if (plan == null)
        {
            return false;
        }

        _context.BalancedWeekPlans.Remove(plan);
        return true;
    }
}
