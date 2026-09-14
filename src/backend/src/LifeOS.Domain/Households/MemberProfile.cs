using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// Consumption profile within a household (e.g. an adult or a child), used to compute portions.
/// Carries the household's usual portion coefficient, adjustable per meal in later milestones
/// (<c>planned_meal_parts</c>).
/// </summary>
public sealed class MemberProfile : Entity
{
    public Guid HouseholdId { get; private set; }
    public string DisplayName { get; private set; }
    public decimal PortionCoefficient { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private MemberProfile(
        Guid id,
        Guid householdId,
        string displayName,
        decimal portionCoefficient,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        DisplayName = displayName;
        PortionCoefficient = portionCoefficient;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static MemberProfile Create(
        Guid householdId,
        string displayName,
        decimal portionCoefficient = 1.0m,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A member profile must belong to a household.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Member profile display name is required.", nameof(displayName));
        }

        if (portionCoefficient <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(portionCoefficient), "Portion coefficient must be strictly positive.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new MemberProfile(Guid.NewGuid(), householdId, displayName.Trim(), portionCoefficient, timestamp, timestamp);
    }

    /// <summary>
    /// Rehydrates a <see cref="MemberProfile"/> from persisted state.
    /// </summary>
    public static MemberProfile Rehydrate(
        Guid id,
        Guid householdId,
        string displayName,
        decimal portionCoefficient,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new MemberProfile(id, householdId, displayName, portionCoefficient, createdAt, updatedAt);
    }

    public void UpdateDetails(string displayName, decimal portionCoefficient, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Member profile display name is required.", nameof(displayName));
        }

        if (portionCoefficient <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(portionCoefficient), "Portion coefficient must be strictly positive.");
        }

        DisplayName = displayName.Trim();
        PortionCoefficient = portionCoefficient;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }
}
