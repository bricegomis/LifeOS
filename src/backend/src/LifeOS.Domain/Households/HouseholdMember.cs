using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// Association between a LifeOS <see cref="UserAccount"/> and a <see cref="Household"/>, with a role.
/// See ADR 0003 (household isolation): a given user account resolves to at most one
/// active household at the MVP.
/// </summary>
public sealed class HouseholdMember : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid UserId { get; private set; }
    public HouseholdRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private HouseholdMember(
        Guid id,
        Guid householdId,
        Guid userId,
        HouseholdRole role,
        DateTimeOffset createdAt)
        : base(id)
    {
        HouseholdId = householdId;
        UserId = userId;
        Role = role;
        CreatedAt = createdAt;
    }

    public static HouseholdMember CreateOwner(Guid householdId, Guid userId, DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A household member must belong to a household.", nameof(householdId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A household member must reference a user account.", nameof(userId));
        }

        return new HouseholdMember(Guid.NewGuid(), householdId, userId, HouseholdRole.Owner, now ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Rehydrates a <see cref="HouseholdMember"/> from persisted state.
    /// </summary>
    public static HouseholdMember Rehydrate(
        Guid id,
        Guid householdId,
        Guid userId,
        HouseholdRole role,
        DateTimeOffset createdAt)
    {
        return new HouseholdMember(id, householdId, userId, role, createdAt);
    }
}
