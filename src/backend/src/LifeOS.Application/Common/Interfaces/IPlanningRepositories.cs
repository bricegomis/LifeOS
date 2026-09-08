using LifeOS.Domain.Planning;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="PlanningRule"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IPlanningRuleRepository
{
    Task<IReadOnlyList<PlanningRule>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    Task AddAsync(PlanningRule rule, CancellationToken cancellationToken = default);

    Task<PlanningRule?> GetByIdAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persistence port for <see cref="FrequencyRule"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IFrequencyRuleRepository
{
    Task<IReadOnlyList<FrequencyRule>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    Task AddAsync(FrequencyRule rule, CancellationToken cancellationToken = default);

    Task<FrequencyRule?> GetByIdAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default);
}
