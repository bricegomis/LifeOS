using LifeOS.Application.Food.Costs;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Application.Tests.Food;

public sealed class RecipeCostsTests
{
    private sealed class FakeData : IRecipeCostData
    {
        public readonly List<Recipe> Recipes = [];
        public readonly List<Product> Products = [];
        public readonly List<IReadOnlyCollection<Guid>> ProductQueries = [];

        public Task<IReadOnlyList<Recipe>> RecipesAsync(Guid householdId, Guid? recipeId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Recipe>>(Recipes
                .Where(r => r.HouseholdId == householdId && !r.IsArchived && (recipeId is null || r.Id == recipeId)).ToList());

        public Task<IReadOnlyList<Product>> ProductsOfFoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct)
        {
            ProductQueries.Add(foodIds);
            return Task.FromResult<IReadOnlyList<Product>>(Products
                .Where(p => p.HouseholdId == householdId && foodIds.Contains(p.FoodItemId)).ToList());
        }
    }

    private static readonly Guid Household = Guid.NewGuid();

    [Fact]
    public async Task Costs_every_active_recipe_with_one_product_query()
    {
        var data = new FakeData();
        var rice = Guid.NewGuid();
        var bowl = Recipe.Create(Household, "Bol", 2, 10);
        bowl.AddIngredient(rice, 300, "g");
        var empty = Recipe.Create(Household, "Vide", 1, 0);
        var archived = Recipe.Create(Household, "Ancien", 1, 0);
        archived.Archive();
        data.Recipes.AddRange([bowl, empty, archived]);
        var product = Product.Create(Household, rice, "Riz 1 kg", 1, "kg");
        product.RecordPrice(Guid.NewGuid(), 2.40m, FixedClock.Today, FixedClock.Default.GetUtcNow());
        data.Products.Add(product);

        var costs = await new RecipeCosts(data).OfAllRecipesAsync(Household, default);

        Assert.Equal([bowl.Id, empty.Id], costs.Select(c => c.RecipeId));
        Assert.Equal(0.72m, costs[0].MinTotal);
        Assert.Single(data.ProductQueries);
    }

    [Fact]
    public async Task A_recipe_of_another_household_has_no_cost()
    {
        var data = new FakeData();
        var recipe = Recipe.Create(Guid.NewGuid(), "Ailleurs", 1, 0);
        data.Recipes.Add(recipe);
        Assert.Null(await new RecipeCosts(data).OfRecipeAsync(Household, recipe.Id, default));
        Assert.Empty(data.ProductQueries);
    }
}
