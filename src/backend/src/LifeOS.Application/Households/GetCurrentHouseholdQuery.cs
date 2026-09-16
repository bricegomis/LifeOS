using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Households;

/// <summary>
/// Use case: read the current user's resolved household with members and member profiles.
/// </summary>
public sealed class GetCurrentHouseholdQuery(IHouseholdRepository householdRepository)
{
    private readonly IHouseholdRepository _householdRepository = householdRepository;

    public async Task<HouseholdDto> ExecuteAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var household = await _householdRepository.GetByIdAsync(householdId, cancellationToken)
            ?? throw new InvalidOperationException($"Resolved household {householdId} could not be loaded.");

        return HouseholdMapper.ToDto(household);
    }
}
