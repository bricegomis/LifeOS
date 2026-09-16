using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// A physical activity session or bike commute declared for a day plan.
/// Used to compute energy expenditure adjustments to the daily nutrition target.
/// </summary>
public sealed class ActivitySession : Entity
{
    public Guid DayPlanId { get; private set; }
    public string Type { get; private set; } // run, bike, strength, walk, etc.
    public string Intensity { get; private set; } // low, moderate, high
    public int DurationMinutes { get; private set; }
    public decimal EstimatedEnergyKcal { get; private set; } // computed based on type, intensity, duration
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ActivitySession(
        Guid id,
        Guid dayPlanId,
        string type,
        string intensity,
        int durationMinutes,
        decimal estimatedEnergyKcal,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        DayPlanId = dayPlanId;
        Type = type;
        Intensity = intensity;
        DurationMinutes = durationMinutes;
        EstimatedEnergyKcal = estimatedEnergyKcal;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static ActivitySession Create(
        Guid dayPlanId,
        string type,
        string intensity,
        int durationMinutes,
        decimal estimatedEnergyKcal,
        DateTimeOffset? now = null)
    {
        if (dayPlanId == Guid.Empty)
        {
            throw new ArgumentException("Day plan ID is required.", nameof(dayPlanId));
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Activity type is required.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(intensity))
        {
            throw new ArgumentException("Activity intensity is required.", nameof(intensity));
        }

        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be positive.");
        }

        if (estimatedEnergyKcal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedEnergyKcal), "Estimated energy cannot be negative.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new ActivitySession(
            Guid.NewGuid(),
            dayPlanId,
            type.ToLowerInvariant(),
            intensity.ToLowerInvariant(),
            durationMinutes,
            estimatedEnergyKcal,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Rehydrates an <see cref="ActivitySession"/> from persisted state.
    /// </summary>
    public static ActivitySession Rehydrate(
        Guid id,
        Guid dayPlanId,
        string type,
        string intensity,
        int durationMinutes,
        decimal estimatedEnergyKcal,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new ActivitySession(id, dayPlanId, type, intensity, durationMinutes, estimatedEnergyKcal, createdAt, updatedAt);
    }

    /// <summary>
    /// Updates session details.
    /// </summary>
    public void UpdateDetails(
        string? type = null,
        string? intensity = null,
        int? durationMinutes = null,
        decimal? estimatedEnergyKcal = null,
        DateTimeOffset? now = null)
    {
        if (!string.IsNullOrWhiteSpace(type))
        {
            Type = type.ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(intensity))
        {
            Intensity = intensity.ToLowerInvariant();
        }

        if (durationMinutes.HasValue && durationMinutes.Value > 0)
        {
            DurationMinutes = durationMinutes.Value;
        }

        if (estimatedEnergyKcal.HasValue && estimatedEnergyKcal.Value >= 0)
        {
            EstimatedEnergyKcal = estimatedEnergyKcal.Value;
        }

        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }
}
