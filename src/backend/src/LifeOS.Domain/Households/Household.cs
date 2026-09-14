using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// The household ("foyer"): the isolated scope that owns every piece of business data
/// (see ADR 0003). Has exactly one owner at the MVP; the model is ready for additional
/// members without a schema change.
/// </summary>
public sealed class Household : Entity
{
    private readonly List<HouseholdMember> _members = [];
    private readonly List<MemberProfile> _memberProfiles = [];

    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<HouseholdMember> Members => _members;
    public IReadOnlyList<MemberProfile> MemberProfiles => _memberProfiles;

    private Household(
        Guid id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a brand-new household with a single owner member and a default member profile,
    /// used both for explicit household creation and for on-demand provisioning of pre-existing
    /// Supabase users on their first authenticated request after this milestone.
    /// </summary>
    public static Household CreateForOwner(Guid supabaseUserId, string name, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Household name is required.", nameof(name));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        var household = new Household(Guid.NewGuid(), name.Trim(), timestamp, timestamp);

        household._members.Add(HouseholdMember.CreateOwner(household.Id, supabaseUserId, timestamp));
        household._memberProfiles.Add(MemberProfile.Create(household.Id, "Moi", 1.0m, timestamp));

        return household;
    }

    /// <summary>
    /// Rehydrates a <see cref="Household"/> from persisted state.
    /// </summary>
    public static Household Rehydrate(
        Guid id,
        string name,
        IEnumerable<HouseholdMember> members,
        IEnumerable<MemberProfile> memberProfiles,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var household = new Household(id, name, createdAt, updatedAt);
        household._members.AddRange(members);
        household._memberProfiles.AddRange(memberProfiles);

        return household;
    }
}
