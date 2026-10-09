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
    public void Product_owns_nutrition_and_prices_without_changing_reference_values()
    {
        var product = FoodItem.CreateManual(Guid.NewGuid(), "Riz", "100g", new LifeOS.Domain.FoodItems.Nutrition(150, 3, 30, 1));
        Assert.False(product.PurchaseUnitConfirmed);
        Assert.Throws<ArgumentException>(() => product.AddPriceEntry(Guid.NewGuid(), 2.5m, DateTimeOffset.UtcNow));
        product.UpdatePurchaseDetails("Achats", GroceryItemUnit.Kilogram);
        product.AddPriceEntry(Guid.NewGuid(), 2.5m, DateTimeOffset.UtcNow);
        Assert.Equal(150, product.Nutrition!.CaloriesPerUnit);
        Assert.Equal("100g", product.ReferenceUnit);
        Assert.Throws<ArgumentException>(() => product.UpdatePurchaseDetails("Changement", GroceryItemUnit.Liter));
        Assert.Equal(GroceryItemUnit.Kilogram, product.Unit);
        Assert.Equal("Achats", product.Description);
        product.Archive();
        Assert.Single(product.PriceHistory);
    }
}
