using LifeOS.Domain.Food.Foods;

namespace LifeOS.Domain.Tests.Food.Foods;

public sealed class FoodItemTests
{
    [Fact]
    public void A_food_needs_its_eight_nutrition_fields_and_zero_counts_as_known()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Riz", "", new Nutrition(0, 0, 0, 0));
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
        Assert.Throws<ArgumentException>(() => FoodItem.CreateManual(Guid.NewGuid(), "Riz", referenceUnit));

    [Fact]
    public void Digestive_values_are_validated_and_notes_stay_out_of_completion()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Oignon", "100 g", new Nutrition(40, 1.1, 7.4, 0.1, 1.7));
        Assert.Throws<ArgumentException>(() => food.UpdateDigestiveInformation("unknown", 1, ""));
        Assert.Throws<ArgumentException>(() => food.UpdateDigestiveInformation(null, 5, ""));
        food.UpdateDigestiveInformation("high", 1, "  ");
        Assert.Null(food.DigestiveNotes);
        Assert.Empty(food.MissingFields);
    }

    [Fact]
    public void Nutrition_values_cannot_be_negative() =>
        Assert.Throws<ArgumentException>(() => new Nutrition(null, null, null, null, -1));
}
