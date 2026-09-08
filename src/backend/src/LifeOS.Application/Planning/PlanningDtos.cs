namespace LifeOS.Application.Planning;

/// <summary>
/// Read model for a <c>PlanningRuleTarget</c>, mirroring the frontend's discriminated union as a
/// flat, JSON-friendly shape (<c>Kind</c> discriminates which other fields are set).
/// </summary>
public sealed record PlanningRuleTargetDto(string Kind, string? ComponentId, string? ComponentType, string? DishId);

/// <summary>
/// Read model returned by the API for a planning rule, mirroring the frontend's
/// <c>PlanningRule</c> shape.
/// </summary>
public sealed record PlanningRuleDto(Guid Id, string Weekday, string MealType, PlanningRuleTargetDto Target);

/// <summary>
/// Read model for a <c>FrequencyRuleTarget</c>, mirroring the frontend's discriminated union as a
/// flat, JSON-friendly shape (<c>Kind</c> discriminates which other fields are set).
/// </summary>
public sealed record FrequencyRuleTargetDto(string Kind, string? ComponentId, string? DishId, string? CategoryId, string? Label);

/// <summary>
/// Read model returned by the API for a frequency rule, mirroring the frontend's
/// <c>FrequencyRule</c> shape.
/// </summary>
public sealed record FrequencyRuleDto(Guid Id, FrequencyRuleTargetDto Target, int TargetCountPerWeek);
