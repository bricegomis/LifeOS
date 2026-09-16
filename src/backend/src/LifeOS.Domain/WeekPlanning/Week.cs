using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// A planned week, conserved after its passage, duplicable.
/// Aggregate root of the WeekPlanning bounded context.
/// </summary>
public sealed class Week : Entity
{
    public Guid HouseholdId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public string Status { get; private set; } // draft, active, past
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation property for EF Core
    public ICollection<DayPlan> DayPlans { get; set; } = [];
    public ICollection<WeekScenario> Scenarios { get; set; } = [];

    private Week(
        Guid id,
        Guid householdId,
        DateOnly startsOn,
        string status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        StartsOn = startsOn;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Week Create(
        Guid householdId,
        DateOnly startsOn,
        string status = "draft",
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A week must belong to a household.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required.", nameof(status));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new Week(
            Guid.NewGuid(),
            householdId,
            startsOn,
            status.ToLowerInvariant(),
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Updates the status of the week.
    /// </summary>
    public void UpdateStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required.", nameof(status));
        }

        Status = status.ToLowerInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rehydrates a <see cref="Week"/> from persisted state.
    /// </summary>
    public static Week Rehydrate(
        Guid id,
        Guid householdId,
        DateOnly startsOn,
        string status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Week(id, householdId, startsOn, status, createdAt, updatedAt);
    }
}
