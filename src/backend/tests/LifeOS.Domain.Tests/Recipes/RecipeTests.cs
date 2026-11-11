using LifeOS.Domain.Recipes;

namespace LifeOS.Domain.Tests.Recipes;

public sealed class RecipeTests
{
    private static Recipe NewRecipe() => Recipe.Create(Guid.NewGuid(), "  Dhal  ", 4, 35, ["plat"], null, "Cuire.");

    [Fact]
    public void Create_trims_the_name_and_keeps_yield_duration_tags_and_instructions()
    {
        var recipe = NewRecipe();
        Assert.Equal("Dhal", recipe.Name);
        Assert.Equal(4, recipe.Servings);
        Assert.Equal(35, recipe.DurationMinutes);
        Assert.Equal(["plat"], recipe.Tags);
        Assert.Equal("Cuire.", recipe.Instructions);
        Assert.False(recipe.IsArchived);
        Assert.Empty(recipe.Ingredients);
    }

    [Theory]
    [InlineData("", 4, 35)]
    [InlineData("   ", 4, 35)]
    [InlineData("Dhal", 0, 35)]
    [InlineData("Dhal", -1, 35)]
    [InlineData("Dhal", 4, -1)]
    public void Create_rejects_a_blank_name_a_non_positive_yield_and_a_negative_duration(string name, int servings, int duration) =>
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), name, servings, duration));

    [Fact]
    public void Create_rejects_an_empty_household_and_oversized_instructions()
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.Empty, "Dhal", 4, 35));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), "Dhal", 4, 35, instructions: new string('x', 10001)));
    }

    [Fact]
    public void A_zero_minute_recipe_is_valid()
    {
        Assert.Equal(0, Recipe.Create(Guid.NewGuid(), "Salade", 2, 0).DurationMinutes);
    }

    [Fact]
    public void AddIngredient_references_a_food_with_an_exact_quantity_and_unit()
    {
        var recipe = NewRecipe();
        var food = Guid.NewGuid();
        recipe.AddIngredient(food, 12.5m, " g ");
        var ingredient = Assert.Single(recipe.Ingredients);
        Assert.Equal(recipe.Id, ingredient.RecipeId);
        Assert.Equal(food, ingredient.FoodItemId);
        Assert.Equal(12.5m, ingredient.Quantity);
        Assert.Equal("g", ingredient.Unit);
    }

    [Theory]
    [InlineData(0, "g")]
    [InlineData(-1, "g")]
    [InlineData(1, " ")]
    [InlineData(0.0000001, "g")]
    [InlineData(1000001, "g")]
    public void AddIngredient_rejects_invalid_quantities_and_blank_units(double quantity, string unit) =>
        Assert.Throws<ArgumentException>(() => NewRecipe().AddIngredient(Guid.NewGuid(), (decimal)quantity, unit));

    [Fact]
    public void AddIngredient_rejects_an_empty_food_id() =>
        Assert.Throws<ArgumentException>(() => NewRecipe().AddIngredient(Guid.Empty, 1, "g"));

    [Fact]
    public void An_ingredient_update_keeps_the_food_and_validates_like_creation()
    {
        var recipe = NewRecipe();
        recipe.AddIngredient(Guid.NewGuid(), 100, "g");
        var ingredient = recipe.Ingredients[0];
        var food = ingredient.FoodItemId;
        ingredient.Update(2, "pièce");
        Assert.Equal(2, ingredient.Quantity);
        Assert.Equal("pièce", ingredient.Unit);
        Assert.Equal(food, ingredient.FoodItemId);
        Assert.Throws<ArgumentException>(() => ingredient.Update(0, "g"));
        Assert.Throws<ArgumentException>(() => ingredient.Update(1, ""));
    }

    [Fact]
    public void Archive_marks_the_recipe_without_touching_its_content()
    {
        var recipe = NewRecipe();
        recipe.AddIngredient(Guid.NewGuid(), 100, "g");
        recipe.Archive();
        Assert.True(recipe.IsArchived);
        Assert.Single(recipe.Ingredients);
    }
}
