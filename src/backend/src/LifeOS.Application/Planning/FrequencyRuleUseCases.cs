using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Planning;

namespace LifeOS.Application.Planning;

/// <summary>
/// Use case: list every frequency rule belonging to the current household.
/// </summary>
public sealed class GetFrequencyRulesQuery(IFrequencyRuleRepository frequencyRuleRepository)
{
    private readonly IFrequencyRuleRepository _frequencyRuleRepository = frequencyRuleRepository;

    public async Task<IReadOnlyList<FrequencyRuleDto>> ExecuteAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var rules = await _frequencyRuleRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

        return rules.Select(PlanningMapper.ToDto).ToList();
    }
}

/// <summary>
/// Use case: create a new weekly frequency constraint for the current household.
/// </summary>
public sealed class CreateFrequencyRuleCommand(IFrequencyRuleRepository frequencyRuleRepository)
{
    private readonly IFrequencyRuleRepository _frequencyRuleRepository = frequencyRuleRepository;

    public async Task<FrequencyRuleDto> ExecuteAsync(
        Guid householdId,
        FrequencyRuleTargetDto target,
        int targetCountPerWeek,
        CancellationToken cancellationToken = default)
    {
        var rule = FrequencyRule.Create(householdId, PlanningMapper.ParseFrequencyRuleTarget(target), targetCountPerWeek);

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
        Guid householdId,
        Guid ruleId,
        int targetCountPerWeek,
        CancellationToken cancellationToken = default)
    {
        var rule = await _frequencyRuleRepository.GetByIdAsync(householdId, ruleId, cancellationToken);

        if (rule is null)
        {
            return null;
        }

        rule.UpdateTargetCount(targetCountPerWeek);

        await _frequencyRuleRepository.UpdateAsync(rule, cancellationToken);

        return PlanningMapper.ToDto(rule);
    }
}

/// <summary>
/// Use case: delete a frequency rule belonging to the current household.
/// </summary>
public sealed class DeleteFrequencyRuleCommand(IFrequencyRuleRepository frequencyRuleRepository)
{
    private readonly IFrequencyRuleRepository _frequencyRuleRepository = frequencyRuleRepository;

    public Task<bool> ExecuteAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default)
        => _frequencyRuleRepository.DeleteAsync(householdId, ruleId, cancellationToken);
}
