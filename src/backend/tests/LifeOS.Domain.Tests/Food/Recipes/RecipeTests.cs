using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Domain.Tests.Food.Recipes;

public sealed class RecipeTests
{
    private static readonly DateTimeOffset Created = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(1);

    private static Recipe NewRecipe() =>
        Recipe.Create(Guid.NewGuid(), "  Dhal  ", 4, 35, ["plat"], "Cuire.", Created);

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
        Assert.Equal(Created, recipe.CreatedAt);
        Assert.Equal(Created, recipe.UpdatedAt);
    }

    [Theory]
    [InlineData("", 4, 35)]
    [InlineData("   ", 4, 35)]
    [InlineData("Dhal", 0, 35)]
    [InlineData("Dhal", -1, 35)]
    [InlineData("Dhal", 4, -1)]
    public void A_blank_name_a_non_positive_yield_and_a_negative_duration_are_rejected(string name, int servings, int duration)
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), name, servings, duration));
        var recipe = NewRecipe();
        Assert.Throws<ArgumentException>(() => recipe.Update(name, servings, duration));
        Assert.Equal("Dhal", recipe.Name);
        Assert.Equal(4, recipe.Servings);
    }

    [Fact]
    public void An_empty_household_an_oversized_name_and_oversized_instructions_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.Empty, "Dhal", 4, 35));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), new string('x', Recipe.MaxNameLength + 1), 4, 35));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), "Dhal", 4, 35,
            instructions: new string('x', Recipe.MaxInstructionsLength + 1)));
    }

    [Fact]
    public void A_zero_minute_recipe_is_valid() =>
        Assert.Equal(0, Recipe.Create(Guid.NewGuid(), "Salade", 2, 0).DurationMinutes);

    [Fact]
    public void Tags_are_trimmed_deduplicated_and_blank_ones_dropped()
    {
        var recipe = Recipe.Create(Guid.NewGuid(), "Dhal", 4, 35, [" plat ", "Plat", "", "batch"]);
        Assert.Equal(["plat", "batch"], recipe.Tags);
    }

    [Fact]
    public void A_tag_cannot_contain_the_storage_separator_be_too_long_or_exceed_the_count()
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), "Dhal", 4, 35, ["plat|four"]));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), "Dhal", 4, 35, [new string('x', Recipe.MaxTagLength + 1)]));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Guid.NewGuid(), "Dhal", 4, 35,
            Enumerable.Range(0, Recipe.MaxTags + 1).Select(i => $"tag{i}")));
    }

    [Fact]
    public void Update_changes_only_the_given_fields()
    {
        var recipe = NewRecipe();
        recipe.Update(servings: 6, now: Later);
        Assert.Equal("Dhal", recipe.Name);
        Assert.Equal(6, recipe.Servings);
        Assert.Equal(35, recipe.DurationMinutes);
        Assert.Equal(["plat"], recipe.Tags);
        Assert.Equal("Cuire.", recipe.Instructions);
        Assert.Equal(Later, recipe.UpdatedAt);
        Assert.Equal(Created, recipe.CreatedAt);
    }

    [Fact]
    public void Update_can_clear_tags_and_instructions_explicitly()
    {
        var recipe = NewRecipe();
        recipe.Update(tags: [], instructions: "  ");
        Assert.Empty(recipe.Tags);
        Assert.Null(recipe.Instructions);
    }

    [Fact]
    public void AddIngredient_references_a_food_with_an_exact_quantity_and_unit()
    {
        var recipe = NewRecipe();
        var food = Guid.NewGuid();
        var ingredient = recipe.AddIngredient(food, 12.5m, " g ", Later);
        Assert.Same(ingredient, Assert.Single(recipe.Ingredients));
        Assert.Equal(recipe.Id, ingredient.RecipeId);
        Assert.Equal(food, ingredient.FoodItemId);
        Assert.Equal(12.5m, ingredient.Quantity);
        Assert.Equal("g", ingredient.Unit);
        Assert.Equal(Later, recipe.UpdatedAt);
    }

    [Theory]
    [InlineData(0, "g")]
    [InlineData(-1, "g")]
    [InlineData(1, " ")]
    [InlineData(1, "tasse")]
    [InlineData(1, "kg")]
    [InlineData(0.0000001, "g")]
    [InlineData(1000001, "g")]
    public void Invalid_quantities_and_units_other_than_g_ml_or_piece_are_rejected_on_add_and_on_update(double quantity, string unit)
    {
        var recipe = NewRecipe();
        Assert.Throws<ArgumentException>(() => recipe.AddIngredient(Guid.NewGuid(), (decimal)quantity, unit));
        Assert.Empty(recipe.Ingredients);
        var line = recipe.AddIngredient(Guid.NewGuid(), 100, "g");
        Assert.Throws<ArgumentException>(() => recipe.UpdateIngredient(line.Id, (decimal)quantity, unit));
        Assert.Equal(100, line.Quantity);
        Assert.Equal("g", line.Unit);
    }

    [Fact]
    public void AddIngredient_rejects_an_empty_food_id() =>
        Assert.Throws<ArgumentException>(() => NewRecipe().AddIngredient(Guid.Empty, 1, "g"));

    [Fact]
    public void UpdateIngredient_keeps_the_food_and_reports_an_unknown_line()
    {
        var recipe = NewRecipe();
        var line = recipe.AddIngredient(Guid.NewGuid(), 100, "g");
        var food = line.FoodItemId;
        Assert.True(recipe.UpdateIngredient(line.Id, 2, "pièce", Later));
        Assert.Equal(2, line.Quantity);
        Assert.Equal("pièce", line.Unit);
        Assert.Equal(food, line.FoodItemId);
        Assert.Equal(Later, recipe.UpdatedAt);
        Assert.False(recipe.UpdateIngredient(Guid.NewGuid(), 1, "g"));
    }

    [Fact]
    public void RemoveIngredient_removes_only_that_line_and_reports_an_unknown_line()
    {
        var recipe = NewRecipe();
        var first = recipe.AddIngredient(Guid.NewGuid(), 100, "g");
        var second = recipe.AddIngredient(Guid.NewGuid(), 2, "pièce");
        Assert.True(recipe.RemoveIngredient(first.Id));
        Assert.Same(second, Assert.Single(recipe.Ingredients));
        Assert.False(recipe.RemoveIngredient(first.Id));
    }

    [Fact]
    public void Archive_marks_the_recipe_without_touching_its_content()
    {
        var recipe = NewRecipe();
        recipe.AddIngredient(Guid.NewGuid(), 100, "g");
        recipe.Archive(Later);
        Assert.True(recipe.IsArchived);
        Assert.Single(recipe.Ingredients);
        Assert.Equal(Later, recipe.UpdatedAt);
    }
}
