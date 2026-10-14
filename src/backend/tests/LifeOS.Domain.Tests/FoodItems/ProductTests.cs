using LifeOS.Domain.Articles;
using LifeOS.Domain.Common;
using LifeOS.Domain.FoodItems;

namespace LifeOS.Domain.Tests.FoodItems;

public sealed class ProductTests
{
    [Theory]
    [InlineData(100, "g", "100g", 1)]
    [InlineData(200, "g", "kilogram", .2)]
    [InlineData(100, "ml", "100ml", 1)]
    [InlineData(250, "ml", "liter", .25)]
    [InlineData(2, "piece", "unit", 2)]
    public void Reference_and_purchase_units_convert_only_explicit_compatible_scales(
        double quantity, string from, string to, double expected) =>
        Assert.Equal((decimal)expected, QuantityConversion.Convert((decimal)quantity, from, to));

    [Theory]
    [InlineData("100g", "liter")]
    [InlineData("piece", "kilogram")]
    [InlineData("ml", "g")]
    [InlineData("tasse", "liter")]
    public void Unknown_dimensions_never_invent_a_conversion(string from, string to) =>
        Assert.Null(QuantityConversion.Convert(1, from, to));

    [Fact]
    public void Food_completeness_distinguishes_zero_null_base_and_digestive_fields()
    {
        var food = FoodItem.CreateManual(Guid.NewGuid(), "Riz", "", new LifeOS.Domain.FoodItems.Nutrition(0, 0, 0, 0));
        Assert.Equal(new[] { "Base nutritionnelle", "FODMAP", "NOVA" }, food.MissingFields);
        food.UpdateDetails("Riz", "100 g", food.Nutrition);
        food.UpdateDigestiveInformation("low", 1, null);
        Assert.Empty(food.MissingFields);
        food.UpdateDetails("Riz", "100 g", new LifeOS.Domain.FoodItems.Nutrition(0, null, 0, 0));
        Assert.Equal("Protéines", Assert.Single(food.MissingFields));
        Assert.Throws<ArgumentException>(() => food.UpdateDigestiveInformation("unknown", 1, ""));
        Assert.Throws<ArgumentException>(() => food.UpdateDigestiveInformation(null, 5, ""));
    }
}
