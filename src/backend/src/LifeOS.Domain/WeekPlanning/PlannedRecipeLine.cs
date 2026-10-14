using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

public sealed class PlannedRecipeLine : Entity
{
    public Guid PlannedMealId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid? RecipeId { get; private set; }
    public int Position { get; private set; }
    public string? Name { get; private set; }
    public string? Instructions { get; private set; }
    public decimal YieldServings { get; private set; }
    public decimal? PlannedServings { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<PlannedRecipeLineIngredient> Ingredients { get; private set; } = [];
    public List<PlannedRecipeLinePortion> Portions { get; private set; } = [];

    public decimal TotalServings => Portions.Count > 0 ? Portions.Sum(p => p.Servings) : PlannedServings ?? 0;
    public bool IsAdHoc => RecipeId is null;

    private PlannedRecipeLine() { }

    public static PlannedRecipeLine Create(Guid plannedMealId, Guid householdId, Guid? recipeId,
        int position, string? name, string? instructions, decimal yieldServings, decimal? plannedServings)
    {
        if (plannedMealId == Guid.Empty || householdId == Guid.Empty)
            throw new ArgumentException("Le repas et le foyer sont obligatoires.");
        if (recipeId == Guid.Empty) throw new ArgumentException("Identifiant de recette invalide.");
        if (position < 0 || position > 100) throw new ArgumentOutOfRangeException(nameof(position));
        if (yieldServings <= 0 || yieldServings > 1000 || decimal.Round(yieldServings, 4) != yieldServings)
            throw new ArgumentException("Le rendement doit être positif et comporter au plus quatre décimales.");
        if (plannedServings is { } servings && (servings <= 0 || servings > 1000 || decimal.Round(servings, 4) != servings))
            throw new ArgumentException("Les portions doivent être positives et comporter au plus quatre décimales.");
        if (name?.Trim().Length > 200 || instructions?.Length > 10000)
            throw new ArgumentException("Le nom ou les instructions dépassent la longueur autorisée.");
        if (recipeId is not null && string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Le nom de la recette est obligatoire.");
        return new PlannedRecipeLine
        {
            Id = Guid.NewGuid(), PlannedMealId = plannedMealId, HouseholdId = householdId, RecipeId = recipeId,
            Position = position, Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Instructions = instructions, YieldServings = yieldServings, PlannedServings = plannedServings,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void SetPortions(decimal? plannedServings, IReadOnlyCollection<PlannedRecipeLinePortion> portions)
    {
        if (portions.Count > 0)
        {
            if (plannedServings is not null)
                throw new ArgumentException("Indiquez un total explicite ou une répartition, pas les deux.");
            if (portions.Select(p => p.MemberProfileId).Distinct().Count() != portions.Count ||
                portions.Any(p => p.PlannedRecipeLineId != Id || p.HouseholdId != HouseholdId))
                throw new ArgumentException("Les portions doivent cibler des profils uniques du même foyer et de la même ligne.");
            PlannedServings = null;
        }
        else
        {
            if (plannedServings is null || plannedServings <= 0 || plannedServings > 1000 ||
                decimal.Round(plannedServings.Value, 4) != plannedServings.Value)
                throw new ArgumentException("Indiquez un total de portions positif.");
            PlannedServings = plannedServings;
        }

        Portions.Clear();
        Portions.AddRange(portions);
    }
}
