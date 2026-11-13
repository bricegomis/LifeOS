using LifeOS.Domain.Food.Foods;

namespace LifeOS.Domain.Tests.Food.Foods;

public sealed class FoodItemTests
{
    [Fact]
    public void A_food_needs_its_eight_nutrition_fields_and_zero_counts_as_known()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Riz", "grain", "", new Nutrition(0, 0, 0, 0));
        Assert.Equal(["Base nutritionnelle", "Fibres", "FODMAP", "NOVA"], food.MissingFields);

        food.UpdateDetails("Riz", "100 g", new Nutrition(0, 0, 0, 0, 0));
        food.UpdateDigestiveInformation("low", 1, null);
        Assert.Empty(food.MissingFields);

        food.UpdateDetails("Riz", "100 g", new Nutrition(0, null, 0, 0, 0));
        Assert.Equal("Protéines", Assert.Single(food.MissingFields));
    }

    [Theory]
    [InlineData("100g")]
    [InlineData("kg")]
    [InlineData("1 portion")]
    public void The_nutrition_base_is_one_of_three_values(string referenceUnit) =>
        Assert.Throws<ArgumentException>(() => FoodItem.CreateManual(Guid.NewGuid(), "Riz", "grain", referenceUnit));

    [Fact]
    public void Digestive_values_are_validated_and_notes_stay_out_of_completion()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Oignon", "vegetable", "100 g", new Nutrition(40, 1.1, 7.4, 0.1, 1.7));
        Assert.Throws<ArgumentException>(() => food.UpdateDigestiveInformation("unknown", 1, ""));
        Assert.Throws<ArgumentException>(() => food.UpdateDigestiveInformation(null, 5, ""));
        food.UpdateDigestiveInformation("high", 1, "  ");
        Assert.Null(food.DigestiveNotes);
        Assert.Empty(food.MissingFields);
    }

    [Fact]
    public void Nutrition_values_cannot_be_negative() =>
        Assert.Throws<ArgumentException>(() => new Nutrition(null, null, null, null, -1));

    [Theory]
    [InlineData("")]
    [InlineData("légume")]
    [InlineData("Vegetable")]
    public void A_food_always_has_a_family_from_the_fixed_list(string family) =>
        Assert.Throws<ArgumentException>(() => FoodItem.CreateManual(Guid.NewGuid(), "Tofu", family, "100 g"));

    [Fact]
    public void Classification_is_normalised_and_stays_out_of_completion()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Tofu", "legume", "100 g", new Nutrition(145, 14, 1.5, 8.5, 1.5));
        food.UpdateDigestiveInformation("low", 3, null);

        food.UpdateClassification("legume", ["fat", "protein", "protein"], "fresh", [12, 1, 12], [" wok ", "Wok", "apéro"]);

        Assert.Equal("legume", food.Family);
        Assert.Equal(["protein", "fat"], food.Roles);
        Assert.Equal("fresh", food.Conservation);
        Assert.Equal([1, 12], food.SeasonMonths);
        Assert.Equal(["wok", "apéro"], food.Tags);
        Assert.Empty(food.MissingFields);
    }

    [Fact]
    public void Classification_rejects_values_outside_the_lists()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Poivron", "vegetable", "100 g");
        Assert.Throws<ArgumentException>(() => food.UpdateClassification("vegetable", ["main"], null, [], []));
        Assert.Throws<ArgumentException>(() => food.UpdateClassification("vegetable", [], "fridge", [], []));
        Assert.Throws<ArgumentException>(() => food.UpdateClassification("vegetable", [], null, [0], []));
        Assert.Throws<ArgumentException>(() => food.UpdateClassification("vegetable", [], null, [13], []));
        Assert.Throws<ArgumentException>(() => food.UpdateClassification("vegetable", [], null, [], [new string('x', 51)]));
        Assert.Throws<ArgumentException>(() => food.UpdateClassification("vegetable", [], null, [],
            Enumerable.Range(0, 21).Select(i => $"t{i}")));
    }

    [Fact]
    public void A_food_without_season_months_is_not_seasonal()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Poivron", "vegetable", "100 g");
        Assert.Null(food.IsInSeason(12));

        food.UpdateClassification("vegetable", ["vegetable"], "fresh", [7, 8, 9, 10], []);
        Assert.False(food.IsInSeason(12));
        Assert.True(food.IsInSeason(8));
    }
}
