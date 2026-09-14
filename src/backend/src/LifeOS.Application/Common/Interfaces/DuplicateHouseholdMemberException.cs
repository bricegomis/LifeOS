namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Thrown by <see cref="IHouseholdRepository.AddAsync"/> when the Supabase user being provisioned
/// already owns a household, concurrently created by another in-flight request. Callers should
/// re-resolve the household instead of treating this as a failure.
/// </summary>
public sealed class DuplicateHouseholdMemberException : Exception
{
    public DuplicateHouseholdMemberException()
        : base("This Supabase user is already associated with a household.")
    {
    }
}
