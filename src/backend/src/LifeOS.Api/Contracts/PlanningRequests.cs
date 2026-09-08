using LifeOS.Application.Planning;

namespace LifeOS.Api.Contracts;

/// <summary>
/// Request body for creating or updating a planning rule.
/// </summary>
public sealed record PlanningRuleRequest(string Weekday, string MealType, PlanningRuleTargetDto Target);

/// <summary>
/// Request body for creating a frequency rule.
/// </summary>
public sealed record FrequencyRuleRequest(FrequencyRuleTargetDto Target, int TargetCountPerWeek);

/// <summary>
/// Request body for updating a frequency rule's weekly target count.
/// </summary>
public sealed record FrequencyRuleTargetCountRequest(int TargetCountPerWeek);
