using LifeOS.Domain.Food;

namespace LifeOS.Domain.Tests.Food;

public sealed class MeasuresTests
{
    [Theory]
    [InlineData(250, "g", Dimension.Mass, 0.25)]
    [InlineData(2, "kg", Dimension.Mass, 2)]
    [InlineData(400, "ml", Dimension.Volume, 0.4)]
    [InlineData(1.5, "L", Dimension.Volume, 1.5)]
    [InlineData(6, "pièce", Dimension.Count, 6)]
    public void Units_convert_only_within_their_dimension(double quantity, string unit, Dimension dimension, double amount) =>
        Assert.Equal(new BaseQuantity(dimension, (decimal)amount), Measures.ToBase((decimal)quantity, unit));

    [Theory]
    [InlineData("tasse")]
    [InlineData("cuillère")]
    [InlineData("")]
    public void Unknown_units_have_no_base(string unit) => Assert.Null(Measures.ToBase(1, unit));

    [Fact]
    public void Nutrition_bases_are_expressed_in_comparison_units()
    {
        Assert.Equal(new BaseQuantity(Dimension.Mass, 0.1m), Measures.NutritionBase("100 g"));
        Assert.Equal(new BaseQuantity(Dimension.Volume, 0.1m), Measures.NutritionBase("100 ml"));
        Assert.Equal(new BaseQuantity(Dimension.Count, 1m), Measures.NutritionBase("1 pièce"));
        Assert.Null(Measures.NutritionBase("100g"));
    }
}
