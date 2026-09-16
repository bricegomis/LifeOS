namespace LifeOS.Application.Households;

/// <summary>
/// Read/write model for a <see cref="LifeOS.Domain.WeekPlanning.ActivitySession"/>.
/// </summary>
public sealed record ActivitySessionDto(
    Guid Id,
    Guid DayPlanId,
    string Type,
    string Intensity,
    int DurationMinutes,
    decimal EstimatedEnergyKcal,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Request model for creating or updating an activity session.
/// </summary>
public sealed record CreateActivitySessionRequest(
    string Type,
    string Intensity,
    int DurationMinutes,
    decimal EstimatedEnergyKcal);

/// <summary>
/// Request model for updating an activity session.
/// </summary>
public sealed record UpdateActivitySessionRequest(
    string? Type,
    string? Intensity,
    int? DurationMinutes,
    decimal? EstimatedEnergyKcal);
