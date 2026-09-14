using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// Association between a Supabase user (the JWT "sub") and a <see cref="Household"/>, with a role.
/// See ADR 0003 (household isolation): a given <c>supabase_user_id</c> resolves to at most one
/// active household at the MVP.
/// </summary>
public sealed class HouseholdMember : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid SupabaseUserId { get; private set; }
    public HouseholdRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private HouseholdMember(
        Guid id,
        Guid householdId,
        Guid supabaseUserId,
        HouseholdRole role,
        DateTimeOffset createdAt)
        : base(id)
    {
        HouseholdId = householdId;
        SupabaseUserId = supabaseUserId;
        Role = role;
        CreatedAt = createdAt;
    }

    public static HouseholdMember CreateOwner(Guid householdId, Guid supabaseUserId, DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A household member must belong to a household.", nameof(householdId));
        }

        if (supabaseUserId == Guid.Empty)
        {
            throw new ArgumentException("A household member must reference a Supabase user.", nameof(supabaseUserId));
        }

        return new HouseholdMember(Guid.NewGuid(), householdId, supabaseUserId, HouseholdRole.Owner, now ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Rehydrates a <see cref="HouseholdMember"/> from persisted state.
    /// </summary>
    public static HouseholdMember Rehydrate(
        Guid id,
        Guid householdId,
        Guid supabaseUserId,
        HouseholdRole role,
        DateTimeOffset createdAt)
    {
        return new HouseholdMember(id, householdId, supabaseUserId, role, createdAt);
    }
}
