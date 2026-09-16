using System.ComponentModel.DataAnnotations;
using LifeOS.Application.Planning;

namespace LifeOS.Api.Contracts;

/// <summary>
/// Request body for creating or updating a planning rule.
/// </summary>
public sealed record PlanningRuleRequest(
    [property: Required, StringLength(20)] string Weekday,
    [property: Required, StringLength(20)] string MealType,
    [property: Required] PlanningRuleTargetDto Target);

/// <summary>
/// Request body for creating a frequency rule.
/// </summary>
public sealed record FrequencyRuleRequest(
    [property: Required] FrequencyRuleTargetDto Target,
    [property: Range(0, int.MaxValue)] int TargetCountPerWeek);

/// <summary>
/// Request body for updating a frequency rule's weekly target count.
/// </summary>
public sealed record FrequencyRuleTargetCountRequest(
    [property: Range(0, int.MaxValue)] int TargetCountPerWeek);
