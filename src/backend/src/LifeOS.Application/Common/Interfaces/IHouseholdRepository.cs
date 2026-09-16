using LifeOS.Domain.Households;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for <see cref="Household"/> aggregates. Implemented in the Infrastructure layer.
/// </summary>
public interface IHouseholdRepository
{
    /// <summary>
    /// Finds the household a Supabase user belongs to, if any (via <see cref="HouseholdMember"/>).
    /// </summary>
    Task<Household?> FindBySupabaseUserIdAsync(Guid supabaseUserId, CancellationToken cancellationToken = default);

    Task<Household?> GetByIdAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task<bool> MemberProfileBelongsToHouseholdAsync(
        Guid householdId,
        Guid memberProfileId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Household household, CancellationToken cancellationToken = default);
}
