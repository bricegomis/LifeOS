using LifeOS.Domain.Planning;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="PlanningRule"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IPlanningRuleRepository
{
    Task<IReadOnlyList<PlanningRule>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task AddAsync(PlanningRule rule, CancellationToken cancellationToken = default);

    Task<PlanningRule?> GetByIdAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default);

    Task UpdateAsync(PlanningRule rule, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persistence port for <see cref="FrequencyRule"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IFrequencyRuleRepository
{
    Task<IReadOnlyList<FrequencyRule>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task AddAsync(FrequencyRule rule, CancellationToken cancellationToken = default);

    Task<FrequencyRule?> GetByIdAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default);

    Task UpdateAsync(FrequencyRule rule, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default);
}
