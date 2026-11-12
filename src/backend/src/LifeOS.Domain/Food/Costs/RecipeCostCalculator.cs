using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Domain.Food.Costs;

/// <summary>One latest price of a product in a store, expressed per kilogram, litre or piece.</summary>
public sealed record PriceCandidate(Guid ProductId, string ProductName, Guid StoreId, decimal PackagePrice,
    decimal UnitPrice, Dimension Dimension, DateOnly ObservedOn);

/// <summary>
/// The cost of one ingredient: the cheapest and the dearest comparable price. Both are null
/// when no price is comparable with the ingredient's unit.
/// </summary>
public sealed record IngredientCost(Guid IngredientId, Guid FoodItemId, decimal Quantity, string Unit,
    PriceCandidate? Cheapest, PriceCandidate? Dearest, decimal? MinAmount, decimal? MaxAmount, int ComparablePrices)
{
    public bool IsKnown => Cheapest is not null;
}

/// <summary>
/// Min–max cost of a recipe, all stores together (ADR 0010, § 5). Totals only add known
/// ingredients; while <see cref="MissingIngredients"/> is above zero they are partial.
/// </summary>
public sealed record RecipeCost(Guid RecipeId, int Servings, IReadOnlyList<IngredientCost> Lines, decimal MinTotal, decimal MaxTotal)
{
    public int MissingIngredients => Lines.Count(line => !line.IsKnown);
    public bool IsComplete => Lines.Count > 0 && MissingIngredients == 0;
    public decimal MinPerServing => MinTotal / Servings;
    public decimal MaxPerServing => MaxTotal / Servings;
}

/// <summary>
/// Computes a recipe cost on read, never stored. For each ingredient, the latest price of each
/// product/store pair of its food (archived products excluded) on the ingredient's dimension:
/// the cheapest gives the minimum, the dearest the maximum. Mass, volume and pieces are never
/// converted into each other. The calculation never picks a product for the recipe.
/// </summary>
public static class RecipeCostCalculator
{
    public static RecipeCost Compute(Recipe recipe, IEnumerable<Product> products)
    {
        var byFood = products.Where(product => !product.IsArchived).ToLookup(product => product.FoodItemId);
        var lines = recipe.Ingredients.Select(ingredient => Line(ingredient, byFood[ingredient.FoodItemId])).ToList();
        return new RecipeCost(recipe.Id, recipe.Servings, lines,
            lines.Sum(line => line.MinAmount ?? 0), lines.Sum(line => line.MaxAmount ?? 0));
    }

    private static IngredientCost Line(RecipeIngredient ingredient, IEnumerable<Product> products)
    {
        var quantity = Measures.ToBase(ingredient.Quantity, ingredient.Unit);
        var candidates = quantity is null ? [] : products
            .SelectMany(product => product.LatestPricePerStore().Select(price => new PriceCandidate(
                product.Id, product.Name, price.StoreId, price.Price, price.UnitPrice, price.Basis.Dimension, price.ObservedOn)))
            .Where(candidate => candidate.Dimension == quantity.Value.Dimension)
            .OrderBy(candidate => candidate.UnitPrice).ThenByDescending(candidate => candidate.ObservedOn).ThenBy(candidate => candidate.ProductId)
            .ToList();
        if (candidates.Count == 0)
            return new IngredientCost(ingredient.Id, ingredient.FoodItemId, ingredient.Quantity, ingredient.Unit, null, null, null, null, 0);
        var cheapest = candidates[0];
        var dearest = candidates[^1];
        var amount = quantity!.Value.Amount;
        return new IngredientCost(ingredient.Id, ingredient.FoodItemId, ingredient.Quantity, ingredient.Unit,
            cheapest, dearest, amount * cheapest.UnitPrice, amount * dearest.UnitPrice, candidates.Count);
    }
}
