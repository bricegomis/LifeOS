using System.ComponentModel.DataAnnotations;

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
    [property: Required, StringLength(50)] string Type,
    [property: Required, StringLength(50)] string Intensity,
    [property: Range(1, int.MaxValue)] int DurationMinutes,
    [property: Range(0, double.MaxValue)] decimal EstimatedEnergyKcal);

/// <summary>
/// Request model for updating an activity session.
/// </summary>
public sealed record UpdateActivitySessionRequest(
    [property: StringLength(50, MinimumLength = 1)] string? Type,
    [property: StringLength(50, MinimumLength = 1)] string? Intensity,
    [property: Range(1, int.MaxValue)] int? DurationMinutes,
    [property: Range(0, double.MaxValue)] decimal? EstimatedEnergyKcal);
