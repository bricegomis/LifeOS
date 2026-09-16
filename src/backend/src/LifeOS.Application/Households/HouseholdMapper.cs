using LifeOS.Domain.Households;

namespace LifeOS.Application.Households;

internal static class HouseholdMapper
{
    public static HouseholdDto ToDto(Household household)
    {
        return new HouseholdDto(
            household.Id,
            household.Name,
            household.Members
                .Select(member => new HouseholdMemberDto(
                    member.Id,
                    member.SupabaseUserId,
                    member.Role.ToString().ToLowerInvariant(),
                    member.CreatedAt))
                .ToList(),
            household.MemberProfiles
                .Select(profile => new MemberProfileDto(
                    profile.Id,
                    profile.DisplayName,
                    profile.PortionCoefficient,
                    profile.CreatedAt,
                    profile.UpdatedAt))
                .ToList(),
            household.CreatedAt,
            household.UpdatedAt);
    }
}
