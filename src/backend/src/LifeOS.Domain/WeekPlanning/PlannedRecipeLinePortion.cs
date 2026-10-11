using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

public sealed class PlannedRecipeLinePortion : Entity
{
    public Guid PlannedRecipeLineId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public decimal Servings { get; private set; }

    private PlannedRecipeLinePortion() { }

    public static PlannedRecipeLinePortion Create(Guid lineId, Guid householdId, Guid profileId, decimal servings)
    {
        if (lineId == Guid.Empty || householdId == Guid.Empty || profileId == Guid.Empty)
            throw new ArgumentException("La ligne, le foyer et le profil sont obligatoires.");
        if (servings <= 0 || servings > 1000 || decimal.Round(servings, 4) != servings)
            throw new ArgumentException("Les portions doivent être positives et comporter au plus quatre décimales.");
        return new PlannedRecipeLinePortion
        {
            Id = Guid.NewGuid(), PlannedRecipeLineId = lineId, HouseholdId = householdId,
            MemberProfileId = profileId, Servings = servings,
        };
    }
}
