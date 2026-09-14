namespace LifeOS.Domain.Households;

/// <summary>
/// Role of a <see cref="HouseholdMember"/> within a <see cref="Household"/>.
/// Only <see cref="Owner"/> is exposed to users at the MVP; <see cref="Member"/> is modelled
/// ahead of time so multi-member households do not require a schema change later.
/// </summary>
public enum HouseholdRole
{
    Owner = 0,
    Member = 1,
}
