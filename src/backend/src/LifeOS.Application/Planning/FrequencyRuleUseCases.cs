using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Planning;

namespace LifeOS.Application.Planning;

/// <summary>
/// Use case: list every frequency rule belonging to the current user.
/// </summary>
public sealed class GetFrequencyRulesQuery(IFrequencyRuleRepository frequencyRuleRepository)
{
    private readonly IFrequencyRuleRepository _frequencyRuleRepository = frequencyRuleRepository;

    public async Task<IReadOnlyList<FrequencyRuleDto>> ExecuteAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var rules = await _frequencyRuleRepository.GetAllForOwnerAsync(ownerId, cancellationToken);

        return rules.Select(PlanningMapper.ToDto).ToList();
    }
}

/// <summary>
/// Use case: create a new weekly frequency constraint for the current user.
/// </summary>
public sealed class CreateFrequencyRuleCommand(IFrequencyRuleRepository frequencyRuleRepository)
{
    private readonly IFrequencyRuleRepository _frequencyRuleRepository = frequencyRuleRepository;

    public async Task<FrequencyRuleDto> ExecuteAsync(
        Guid ownerId,
        FrequencyRuleTargetDto target,
        int targetCountPerWeek,
        CancellationToken cancellationToken = default)
    {
        var rule = FrequencyRule.Create(ownerId, PlanningMapper.ParseFrequencyRuleTarget(target), targetCountPerWeek);

        await _frequencyRuleRepository.AddAsync(rule, cancellationToken);

        return PlanningMapper.ToDto(rule);
    }
}

/// <summary>
/// Use case: update the weekly target count of an existing frequency rule.
/// </summary>
public sealed class UpdateFrequencyRuleCommand(IFrequencyRuleRepository frequencyRuleRepository)
{
    private readonly IFrequencyRuleRepository _frequencyRuleRepository = frequencyRuleRepository;

    public async Task<FrequencyRuleDto?> ExecuteAsync(
        Guid ownerId,
        Guid ruleId,
        int targetCountPerWeek,
        CancellationToken cancellationToken = default)
    {
        var rule = await _frequencyRuleRepository.GetByIdAsync(ownerId, ruleId, cancellationToken);

        if (rule is null)
        {
            return null;
        }

        rule.UpdateTargetCount(targetCountPerWeek);

        return PlanningMapper.ToDto(rule);
    }
}

/// <summary>
/// Use case: delete a frequency rule belonging to the current user.
/// </summary>
public sealed class DeleteFrequencyRuleCommand(IFrequencyRuleRepository frequencyRuleRepository)
{
    private readonly IFrequencyRuleRepository _frequencyRuleRepository = frequencyRuleRepository;

    public Task<bool> ExecuteAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default)
        => _frequencyRuleRepository.DeleteAsync(ownerId, ruleId, cancellationToken);
}
