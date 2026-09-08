using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Planning;

namespace LifeOS.Application.Planning;

/// <summary>
/// Use case: list every planning rule belonging to the current user.
/// </summary>
public sealed class GetPlanningRulesQuery(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public async Task<IReadOnlyList<PlanningRuleDto>> ExecuteAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var rules = await _planningRuleRepository.GetAllForOwnerAsync(ownerId, cancellationToken);

        return rules.Select(PlanningMapper.ToDto).ToList();
    }
}

/// <summary>
/// Use case: pin a meal component or dish to a weekday/meal slot for the current user.
/// </summary>
public sealed class CreatePlanningRuleCommand(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public async Task<PlanningRuleDto> ExecuteAsync(
        Guid ownerId,
        string weekday,
        string mealType,
        PlanningRuleTargetDto target,
        CancellationToken cancellationToken = default)
    {
        var rule = PlanningRule.Create(
            ownerId,
            PlanningMapper.ParseWeekday(weekday),
            PlanningMapper.ParseMealType(mealType),
            PlanningMapper.ParsePlanningRuleTarget(target));

        await _planningRuleRepository.AddAsync(rule, cancellationToken);

        return PlanningMapper.ToDto(rule);
    }
}

/// <summary>
/// Use case: update an existing planning rule belonging to the current user.
/// </summary>
public sealed class UpdatePlanningRuleCommand(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public async Task<PlanningRuleDto?> ExecuteAsync(
        Guid ownerId,
        Guid ruleId,
        string weekday,
        string mealType,
        PlanningRuleTargetDto target,
        CancellationToken cancellationToken = default)
    {
        var rule = await _planningRuleRepository.GetByIdAsync(ownerId, ruleId, cancellationToken);

        if (rule is null)
        {
            return null;
        }

        rule.Update(
            PlanningMapper.ParseWeekday(weekday),
            PlanningMapper.ParseMealType(mealType),
            PlanningMapper.ParsePlanningRuleTarget(target));

        return PlanningMapper.ToDto(rule);
    }
}

/// <summary>
/// Use case: delete a planning rule belonging to the current user.
/// </summary>
public sealed class DeletePlanningRuleCommand(IPlanningRuleRepository planningRuleRepository)
{
    private readonly IPlanningRuleRepository _planningRuleRepository = planningRuleRepository;

    public Task<bool> ExecuteAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default)
        => _planningRuleRepository.DeleteAsync(ownerId, ruleId, cancellationToken);
}
