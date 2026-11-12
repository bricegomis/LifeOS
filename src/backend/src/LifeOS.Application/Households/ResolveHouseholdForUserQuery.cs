using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Households;

namespace LifeOS.Application.Households;

/// <summary>
/// Use case: resolve the <see cref="Household"/> a user account belongs to, provisioning a new
/// owner household on demand if the user has never been resolved before (see ADR 0003).
/// </summary>
public sealed class ResolveHouseholdForUserQuery(IHouseholdRepository householdRepository)
{
    private readonly IHouseholdRepository _householdRepository = householdRepository;

    public async Task<Guid> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existing = await _householdRepository.FindByUserIdAsync(userId, cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var household = Household.CreateForOwner(userId, "Mon foyer");

        try
        {
            await _householdRepository.AddAsync(household, cancellationToken);
        }
        catch (DuplicateHouseholdMemberException)
        {
            // Another concurrent request provisioned the household first; re-resolve it.
            var reResolved = await _householdRepository.FindByUserIdAsync(userId, cancellationToken);

            if (reResolved is null)
            {
                throw;
            }

            return reResolved.Id;
        }

        return household.Id;
    }
}
