using LifeOS.Domain.Food.Costs;
using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Application.Food.Contracts;

/// <summary>Reads the Food read contract needs; implemented by Infrastructure on the food tables.</summary>
public interface IFoodReadData
{
    /// <summary>The household's active recipes with their ingredients; only these when <paramref name="recipeIds"/> is given.</summary>
    Task<IReadOnlyList<Recipe>> RecipesAsync(Guid householdId, IReadOnlyCollection<Guid>? recipeIds, CancellationToken ct);
    /// <summary>The household's foods with these ids, archived ones included.</summary>
    Task<IReadOnlyList<FoodItem>> FoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct);
    /// <summary>The household's products of these foods, with their prices.</summary>
    Task<IReadOnlyList<Product>> ProductsOfFoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct);
}

/// <param name="InSeason">In season this month (Paris); null for a food that is not seasonal.</param>
public sealed record FoodRecipeIngredientDto(Guid FoodItemId, string FoodName, decimal Quantity, string Unit, bool? InSeason);

/// <param name="IsPartial">Only the known values were added.</param>
public sealed record FoodNutrientDto(string Nutrient, string Unit, double Value, bool IsPartial);

/// <summary>
/// A recipe as the planner snapshots it for a planned meal: ingredients for the whole yield, nutrition and
/// min–max cost per serving, all computed on read.
/// </summary>
/// <param name="NutritionComplete">Every ingredient has all five nutrients on a matching base.</param>
/// <param name="MinCostPerServing">Cheapest cost per serving across stores; null when no ingredient has a price.</param>
/// <param name="CostComplete">Every ingredient has a price on its dimension.</param>
/// <param name="OutOfSeason">Seasonal ingredients that are not in season this month.</param>
public sealed record FoodRecipeDto(Guid Id, string Name, int Servings, int DurationMinutes, IReadOnlyList<string> Tags,
    IReadOnlyList<FoodRecipeIngredientDto> Ingredients, IReadOnlyList<FoodNutrientDto> NutritionPerServing, bool NutritionComplete,
    decimal? MinCostPerServing, decimal? MaxCostPerServing, bool CostComplete, int OutOfSeason);

/// <summary>
/// Published read contract of the Food module (ADR 0009, ADR 0010, ADR 0012), for the future planner: the
/// active recipes with what a planned meal keeps (ingredients, nutrition, cost, season).
/// </summary>
public sealed class FoodReadContract(IFoodReadData data, TimeProvider clock)
{
    public DateOnly Today() => FoodClock.Today(clock);

    /// <summary>The household's active recipes by name; only <paramref name="recipeIds"/> when given (unknown ids are left out).</summary>
    public async Task<IReadOnlyList<FoodRecipeDto>> RecipesAsync(Guid householdId, IReadOnlyCollection<Guid>? recipeIds, CancellationToken ct)
    {
        var recipes = await data.RecipesAsync(householdId, recipeIds, ct);
        var foodIds = recipes.SelectMany(r => r.Ingredients).Select(i => i.FoodItemId).ToHashSet();
        var foods = foodIds.Count == 0 ? new Dictionary<Guid, FoodItem>()
            : (await data.FoodsAsync(householdId, foodIds, ct)).ToDictionary(f => f.Id);
        var products = foodIds.Count == 0 ? [] : await data.ProductsOfFoodsAsync(householdId, foodIds, ct);
        var month = Today().Month;
        return recipes.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(recipe =>
        {
            var nutrition = RecipeNutritionCalculator.Compute(recipe, foods);
            var cost = RecipeCostCalculator.Compute(recipe, products);
            var priced = cost.Lines.Any(l => l.IsKnown);
            var ingredients = recipe.Ingredients.Select(i =>
            {
                var food = foods.GetValueOrDefault(i.FoodItemId);
                return new FoodRecipeIngredientDto(i.FoodItemId, food?.Name ?? "", i.Quantity, i.Unit, food?.IsInSeason(month));
            }).ToList();
            return new FoodRecipeDto(recipe.Id, recipe.Name, recipe.Servings, recipe.DurationMinutes, recipe.Tags, ingredients,
                nutrition.PerServing.Select(n => new FoodNutrientDto(n.Nutrient, n.Unit, n.Value, n.IsPartial)).ToList(),
                nutrition.IsComplete && recipe.Ingredients.Count > 0,
                priced ? cost.MinPerServing : null, priced ? cost.MaxPerServing : null, cost.IsComplete,
                ingredients.Count(i => i.InSeason == false));
        }).ToList();
    }
}
