using LifeOS.Domain.Households;

namespace LifeOS.Domain.Tests.Households;

public class HouseholdTests
{
    [Fact]
    public void CreateForOwner_creates_a_single_owner_member_and_a_default_profile()
    {
        var supabaseUserId = Guid.NewGuid();

        var household = Household.CreateForOwner(supabaseUserId, "Foyer de test");

        Assert.Equal("Foyer de test", household.Name);
        Assert.Single(household.Members);
        Assert.Equal(supabaseUserId, household.Members[0].SupabaseUserId);
        Assert.Equal(HouseholdRole.Owner, household.Members[0].Role);
        Assert.Equal(household.Id, household.Members[0].HouseholdId);
        Assert.Single(household.MemberProfiles);
        Assert.Equal(household.Id, household.MemberProfiles[0].HouseholdId);
        Assert.Equal(1.0m, household.MemberProfiles[0].PortionCoefficient);
    }

    [Fact]
    public void CreateForOwner_rejects_blank_name()
    {
        Assert.Throws<ArgumentException>(() => Household.CreateForOwner(Guid.NewGuid(), "   "));
    }

    [Fact]
    public void Rehydrate_restores_members_and_profiles()
    {
        var householdId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var member = HouseholdMember.CreateOwner(householdId, Guid.NewGuid(), now);
        var profile = MemberProfile.Create(householdId, "Moi", 1.0m, now);

        var household = Household.Rehydrate(householdId, "Foyer", [member], [profile], now, now);

        Assert.Equal(householdId, household.Id);
        Assert.Single(household.Members);
        Assert.Single(household.MemberProfiles);
    }
}

public class HouseholdMemberTests
{
    [Fact]
    public void CreateOwner_rejects_empty_household_id()
    {
        Assert.Throws<ArgumentException>(() => HouseholdMember.CreateOwner(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void CreateOwner_rejects_empty_supabase_user_id()
    {
        Assert.Throws<ArgumentException>(() => HouseholdMember.CreateOwner(Guid.NewGuid(), Guid.Empty));
    }
}

public class MemberProfileTests
{
    [Fact]
    public void Create_rejects_non_positive_portion_coefficient()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MemberProfile.Create(Guid.NewGuid(), "Enfant", 0m));
    }

    [Fact]
    public void UpdateDetails_trims_display_name_and_bumps_updated_at()
    {
        var profile = MemberProfile.Create(Guid.NewGuid(), "Enfant", 1.0m, DateTimeOffset.UnixEpoch);

        var later = DateTimeOffset.UnixEpoch.AddDays(1);
        profile.UpdateDetails("  Ado  ", 1.5m, later);

        Assert.Equal("Ado", profile.DisplayName);
        Assert.Equal(1.5m, profile.PortionCoefficient);
        Assert.Equal(later, profile.UpdatedAt);
    }
}
