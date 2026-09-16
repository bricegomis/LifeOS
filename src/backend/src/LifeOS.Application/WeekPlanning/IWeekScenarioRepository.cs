using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Application.WeekPlanning;

/// <summary>
/// Repository for week scenarios.
/// </summary>
public interface IWeekScenarioRepository
{
    Task<WeekScenario?> GetByIdAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeekScenario>> GetAllForWeekAsync(Guid weekId, CancellationToken cancellationToken = default);
    Task<WeekScenario> AddAsync(WeekScenario scenario, CancellationToken cancellationToken = default);
    Task<WeekScenario> UpdateAsync(WeekScenario scenario, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}
