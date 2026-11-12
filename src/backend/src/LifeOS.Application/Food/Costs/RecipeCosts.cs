using LifeOS.Domain.Food.Costs;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Application.Food.Costs;

/// <summary>Data the cost use case reads; implemented by Infrastructure on the Food tables.</summary>
public interface IRecipeCostData
{
    /// <summary>The household's active recipes with their ingredients; one recipe when <paramref name="recipeId"/> is given.</summary>
    Task<IReadOnlyList<Recipe>> RecipesAsync(Guid householdId, Guid? recipeId, CancellationToken ct);
    /// <summary>The household's products of these foods, with their prices.</summary>
    Task<IReadOnlyList<Product>> ProductsOfFoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct);
}

/// <summary>Min–max recipe costs computed on read (ADR 0010, § 5); nothing is stored.</summary>
public sealed class RecipeCosts(IRecipeCostData data)
{
    /// <returns>The cost of the recipe, or null when it is not an active recipe of the household.</returns>
    public async Task<RecipeCost?> OfRecipeAsync(Guid householdId, Guid recipeId, CancellationToken ct) =>
        (await ComputeAsync(householdId, recipeId, ct)).SingleOrDefault();

    /// <summary>The cost of every active recipe of the household, for the recipe list.</summary>
    public Task<IReadOnlyList<RecipeCost>> OfAllRecipesAsync(Guid householdId, CancellationToken ct) =>
        ComputeAsync(householdId, null, ct);

    private async Task<IReadOnlyList<RecipeCost>> ComputeAsync(Guid householdId, Guid? recipeId, CancellationToken ct)
    {
        var recipes = await data.RecipesAsync(householdId, recipeId, ct);
        var foodIds = recipes.SelectMany(recipe => recipe.Ingredients).Select(ingredient => ingredient.FoodItemId).ToHashSet();
        var products = foodIds.Count == 0 ? [] : await data.ProductsOfFoodsAsync(householdId, foodIds, ct);
        return recipes.Select(recipe => RecipeCostCalculator.Compute(recipe, products)).ToList();
    }
}
