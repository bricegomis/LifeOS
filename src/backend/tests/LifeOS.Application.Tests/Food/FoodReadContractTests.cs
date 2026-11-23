using LifeOS.Application.Food.Contracts;
using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Application.Tests.Food;

public sealed class FoodReadContractTests
{
    private static readonly Guid Household = Guid.NewGuid();
    // 2026-11-30 23:30 UTC is already 1 December in Paris.
    private static readonly FixedClock LastNight = new(new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.Zero));

    private sealed class FakeData : IFoodReadData
    {
        public List<Recipe> Recipes { get; } = [];
        public List<FoodItem> Foods { get; } = [];
        public List<Product> Products { get; } = [];

        public Task<IReadOnlyList<Recipe>> RecipesAsync(Guid householdId, IReadOnlyCollection<Guid>? recipeIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Recipe>>(Recipes.Where(r => r.HouseholdId == householdId && !r.IsArchived
                && (recipeIds is null || recipeIds.Contains(r.Id))).ToList());
        public Task<IReadOnlyList<FoodItem>> FoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FoodItem>>(Foods.Where(f => f.HouseholdId == householdId && foodIds.Contains(f.Id)).ToList());
        public Task<IReadOnlyList<Product>> ProductsOfFoodsAsync(Guid householdId, IReadOnlyCollection<Guid> foodIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Product>>(Products.Where(p => p.HouseholdId == householdId && foodIds.Contains(p.FoodItemId)).ToList());
    }

    private static FoodItem Food(string name, Nutrition nutrition, params int[] season)
    {
        var food = FoodItem.CreateManual(Household, name, "vegetable", "100 g", nutrition);
        food.UpdateClassification("vegetable", null, null, season, null);
        return food;
    }

    [Fact]
    public void The_food_day_is_the_day_in_Paris() =>
        Assert.Equal(new DateOnly(2026, 12, 1), new FoodReadContract(new FakeData(), LastNight).Today());

    [Fact]
    public async Task A_recipe_carries_its_ingredients_nutrition_cost_and_season()
    {
        var data = new FakeData();
        var pumpkin = Food("Potiron", new Nutrition(26, 1, 6, 0.1, 0.5), 9, 10, 11, 12);
        var tomato = Food("Tomate", new Nutrition(18, 0.9, 3.9, 0.2, null), 6, 7, 8, 9);
        var onion = Food("Oignon", new Nutrition(40, 1.1, 9, 0.1, 1.7));
        data.Foods.AddRange([pumpkin, tomato, onion]);
        var soup = Recipe.Create(Household, "Soupe", 2, 30, ["hiver"]);
        soup.AddIngredient(pumpkin.Id, 400, "g");
        soup.AddIngredient(tomato.Id, 200, "g");
        soup.AddIngredient(onion.Id, 100, "g");
        var archived = Recipe.Create(Household, "Ancienne", 1, 0);
        archived.Archive();
        data.Recipes.AddRange([soup, archived, Recipe.Create(Guid.NewGuid(), "Autre foyer", 1, 0)]);
        var product = Product.Create(Household, pumpkin.Id, "Potiron 1 kg", 1, "kg");
        product.RecordPrice(Guid.NewGuid(), 3m, FixedClock.Today, FixedClock.Default.GetUtcNow());
        data.Products.Add(product);

        var recipe = Assert.Single(await new FoodReadContract(data, LastNight).RecipesAsync(Household, null, CancellationToken.None));

        Assert.Equal(("Soupe", 2, 30, "hiver"), (recipe.Name, recipe.Servings, recipe.DurationMinutes, recipe.Tags.Single()));
        Assert.Equal([("Potiron", 400m, true), ("Tomate", 200m, false), ("Oignon", 100m, (bool?)null)],
            recipe.Ingredients.Select(i => (i.FoodName, i.Quantity, i.InSeason)));
        Assert.Equal(1, recipe.OutOfSeason);
        var calories = recipe.NutritionPerServing.Single(n => n.Nutrient == "Calories");
        Assert.Equal((90d, false), (Math.Round(calories.Value, 6), calories.IsPartial));
        Assert.True(recipe.NutritionPerServing.Single(n => n.Nutrient == "Fibres").IsPartial);
        Assert.False(recipe.NutritionComplete);
        Assert.Equal((0.60m, 0.60m, false), (recipe.MinCostPerServing, recipe.MaxCostPerServing, recipe.CostComplete));
    }

    [Fact]
    public async Task Only_the_asked_recipes_are_read_and_a_recipe_without_price_has_no_cost()
    {
        var data = new FakeData();
        var a = Recipe.Create(Household, "B recette", 1, 0);
        var b = Recipe.Create(Household, "A recette", 1, 0);
        data.Recipes.AddRange([a, b, Recipe.Create(Household, "C recette", 1, 0)]);

        var recipes = await new FoodReadContract(data, LastNight).RecipesAsync(Household, [a.Id, b.Id, Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(["A recette", "B recette"], recipes.Select(r => r.Name));
        Assert.All(recipes, r => Assert.Equal(((decimal?)null, false, false), (r.MinCostPerServing, r.CostComplete, r.NutritionComplete)));
    }
}
