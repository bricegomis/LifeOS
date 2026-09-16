using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Planning;

namespace LifeOS.Application.Planning;

/// <summary>
/// Use case: list every planning rule belonging to the current household.
/// </summary>
public sealed class GetPlanningRulesQuery(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public async Task<IReadOnlyList<PlanningRuleDto>> ExecuteAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var rules = await _planningRuleRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

        return rules.Select(PlanningMapper.ToDto).ToList();
    }
}

/// <summary>
/// Use case: pin a meal component or dish to a weekday/meal slot for the current household.
/// </summary>
public sealed class CreatePlanningRuleCommand(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public async Task<PlanningRuleDto> ExecuteAsync(
        Guid householdId,
        string weekday,
        string mealType,
        PlanningRuleTargetDto target,
        CancellationToken cancellationToken = default)
    {
        var rule = PlanningRule.Create(
            householdId,
            PlanningMapper.ParseWeekday(weekday),
            PlanningMapper.ParseMealType(mealType),
            PlanningMapper.ParsePlanningRuleTarget(target));

        await _planningRuleRepository.AddAsync(rule, cancellationToken);

        return PlanningMapper.ToDto(rule);
    }
}

/// <summary>
/// Use case: update an existing planning rule belonging to the current household.
/// </summary>
public sealed class UpdatePlanningRuleCommand(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public async Task<PlanningRuleDto?> ExecuteAsync(
        Guid householdId,
        Guid ruleId,
        string weekday,
        string mealType,
        PlanningRuleTargetDto target,
        CancellationToken cancellationToken = default)
    {
        var rule = await _planningRuleRepository.GetByIdAsync(householdId, ruleId, cancellationToken);

        if (rule is null)
        {
            return null;
        }

        rule.Update(
            PlanningMapper.ParseWeekday(weekday),
            PlanningMapper.ParseMealType(mealType),
            PlanningMapper.ParsePlanningRuleTarget(target));

        await _planningRuleRepository.UpdateAsync(rule, cancellationToken);

        return PlanningMapper.ToDto(rule);
    }
}

/// <summary>
/// Use case: delete a planning rule belonging to the current household.
/// </summary>
public sealed class DeletePlanningRuleCommand(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public Task<bool> ExecuteAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default)
        => _planningRuleRepository.DeleteAsync(householdId, ruleId, cancellationToken);
}
