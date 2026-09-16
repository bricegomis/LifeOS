using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// A portion calculated per member for a planned meal.
/// Required when multiple members with different portion coefficients share a planned meal.
/// </summary>
public sealed class PlannedMealPart : Entity
{
    public Guid PlannedMealId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public decimal PortionMultiplier { get; private set; }

    private PlannedMealPart(
        Guid id,
        Guid plannedMealId,
        Guid memberProfileId,
        decimal portionMultiplier)
        : base(id)
    {
        PlannedMealId = plannedMealId;
        MemberProfileId = memberProfileId;
        PortionMultiplier = portionMultiplier;
    }

    public static PlannedMealPart Create(
        Guid plannedMealId,
        Guid memberProfileId,
        decimal portionMultiplier)
    {
        if (plannedMealId == Guid.Empty)
        {
            throw new ArgumentException("Planned meal ID is required.", nameof(plannedMealId));
        }

        if (memberProfileId == Guid.Empty)
        {
            throw new ArgumentException("Member profile ID is required.", nameof(memberProfileId));
        }

        if (portionMultiplier <= 0)
        {
            throw new ArgumentException("Portion multiplier must be greater than 0.", nameof(portionMultiplier));
        }

        return new PlannedMealPart(Guid.NewGuid(), plannedMealId, memberProfileId, portionMultiplier);
    }

    /// <summary>
    /// Rehydrates a <see cref="PlannedMealPart"/> from persisted state.
    /// </summary>
    public static PlannedMealPart Rehydrate(
        Guid id,
        Guid plannedMealId,
        Guid memberProfileId,
        decimal portionMultiplier)
    {
        return new PlannedMealPart(id, plannedMealId, memberProfileId, portionMultiplier);
    }

    /// <summary>
    /// Updates the portion multiplier.
    /// </summary>
    public void Update(decimal portionMultiplier)
    {
        if (portionMultiplier <= 0)
        {
            throw new ArgumentException("Portion multiplier must be greater than 0.", nameof(portionMultiplier));
        }

        PortionMultiplier = portionMultiplier;
    }
}
