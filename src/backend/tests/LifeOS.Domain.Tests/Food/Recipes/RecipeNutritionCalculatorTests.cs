using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Domain.Tests.Food.Recipes;

public sealed class RecipeNutritionCalculatorTests
{
    private static readonly Guid Household = Guid.NewGuid();

    [Fact]
    public void Nutrition_per_serving_is_quantity_times_value_per_base_divided_by_the_yield()
    {
        var rice = FoodItem.CreateManual(Household, "Riz", "100 g", new Nutrition(350, 7, 77, 1, 2));
        var egg = FoodItem.CreateManual(Household, "Œuf", "1 pièce", new Nutrition(70, 6, 0, 5, 0));
        var recipe = Recipe.Create(Household, "Riz aux œufs", 2, 20);
        recipe.AddIngredient(rice.Id, 200, "g");
        recipe.AddIngredient(egg.Id, 4, "pièce");

        var nutrition = RecipeNutritionCalculator.Compute(recipe, Foods(rice, egg));

        Assert.True(nutrition.IsComplete);
        var calories = nutrition.PerServing.Single(n => n.Nutrient == "Calories");
        // (2 × 350 + 4 × 70) / 2
        Assert.Equal(490, calories.Value, 6);
        Assert.False(calories.IsPartial);
        Assert.Equal(0.5 * (2 * 2 + 0), nutrition.PerServing.Single(n => n.Nutrient == "Fibres").Value, 6);
    }

    [Fact]
    public void Unknown_values_and_other_dimensions_stay_unknown_and_make_the_total_partial()
    {
        var milk = FoodItem.CreateManual(Household, "Lait", "100 ml", new Nutrition(46, 3.3, 4.8, 1.6, null));
        var apple = FoodItem.CreateManual(Household, "Pomme", "1 pièce", new Nutrition(80, 0.4, 19, 0.3, 3));
        var recipe = Recipe.Create(Household, "Compote", 1, 10);
        recipe.AddIngredient(milk.Id, 200, "ml");
        // Grams of a food described per piece: no piece weight is invented.
        recipe.AddIngredient(apple.Id, 150, "g");

        var nutrition = RecipeNutritionCalculator.Compute(recipe, Foods(milk, apple));

        Assert.False(nutrition.IsComplete);
        Assert.Equal(new[] { milk.Id, apple.Id }.Order(), nutrition.IncompleteFoodIds.Order());
        var calories = nutrition.PerServing.Single(n => n.Nutrient == "Calories");
        Assert.Equal(92, calories.Value, 6);
        Assert.True(calories.IsPartial);
        Assert.True(nutrition.PerServing.Single(n => n.Nutrient == "Fibres").IsPartial);
    }

    [Fact]
    public void A_missing_food_makes_every_nutrient_partial()
    {
        var recipe = Recipe.Create(Household, "Mystère", 1, 0);
        var missing = Guid.NewGuid();
        recipe.AddIngredient(missing, 100, "g");

        var nutrition = RecipeNutritionCalculator.Compute(recipe, new Dictionary<Guid, FoodItem>());

        Assert.All(nutrition.PerServing, n => Assert.True(n.IsPartial));
        Assert.Equal([missing], nutrition.IncompleteFoodIds);
    }

    private static Dictionary<Guid, FoodItem> Foods(params FoodItem[] foods) => foods.ToDictionary(f => f.Id);
}
