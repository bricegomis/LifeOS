using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Application.WeekPlanning;

/// <summary>
/// Repository for the balanced plans computed for a week.
/// </summary>
public interface IBalancedWeekPlanRepository
{
    Task<BalancedWeekPlan?> GetByIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BalancedWeekPlan>> GetAllForWeekAsync(Guid weekId, CancellationToken cancellationToken = default);
    Task<BalancedWeekPlan> AddAsync(BalancedWeekPlan plan, CancellationToken cancellationToken = default);
    Task<BalancedWeekPlan> UpdateAsync(BalancedWeekPlan plan, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid planId, CancellationToken cancellationToken = default);
}
