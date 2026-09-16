using LifeOS.Application.WeekPlanning;
using LifeOS.Domain.WeekPlanning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.WeekPlanning;

/// <summary>
/// EF Core implementation of week scenario repository.
/// </summary>
public sealed class EfWeekScenarioRepository : IWeekScenarioRepository
{
    private readonly LifeOSDbContext _context;

    public EfWeekScenarioRepository(LifeOSDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<WeekScenario?> GetByIdAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        if (scenarioId == Guid.Empty)
        {
            throw new ArgumentException("Scenario ID is required.", nameof(scenarioId));
        }

        return await _context.WeekScenarios
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);
    }

    public async Task<IReadOnlyList<WeekScenario>> GetAllForWeekAsync(Guid weekId, CancellationToken cancellationToken = default)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        return await _context.WeekScenarios
            .Where(s => s.WeekId == weekId)
            .ToListAsync(cancellationToken);
    }

    public async Task<WeekScenario> AddAsync(WeekScenario scenario, CancellationToken cancellationToken = default)
    {
        if (scenario == null)
        {
            throw new ArgumentNullException(nameof(scenario));
        }

        _context.WeekScenarios.Add(scenario);
        return scenario;
    }

    public async Task<WeekScenario> UpdateAsync(WeekScenario scenario, CancellationToken cancellationToken = default)
    {
        if (scenario == null)
        {
            throw new ArgumentNullException(nameof(scenario));
        }

        _context.WeekScenarios.Update(scenario);
        return scenario;
    }

    public async Task<bool> DeleteAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        if (scenarioId == Guid.Empty)
        {
            throw new ArgumentException("Scenario ID is required.", nameof(scenarioId));
        }

        var scenario = await GetByIdAsync(scenarioId, cancellationToken);
        if (scenario == null)
        {
            return false;
        }

        _context.WeekScenarios.Remove(scenario);
        return true;
    }
}
