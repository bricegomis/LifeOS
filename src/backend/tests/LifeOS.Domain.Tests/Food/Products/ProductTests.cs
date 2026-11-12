using LifeOS.Domain.Food;
using LifeOS.Domain.Food.Products;

namespace LifeOS.Domain.Tests.Food.Products;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static Product Emmental(decimal quantity = 400, string unit = "g") =>
        Product.Create(Guid.NewGuid(), Guid.NewGuid(), "Emmental râpé", quantity, unit, now: Now);

    [Theory]
    [InlineData(0, "g")]
    [InlineData(400, "")]
    [InlineData(400, "sachet")]
    [InlineData(-1, "kg")]
    public void A_product_only_exists_with_its_package_contents(decimal quantity, string unit) =>
        Assert.Throws<ArgumentException>(() => Emmental(quantity, unit));

    [Fact]
    public void A_product_belongs_to_a_food() =>
        Assert.Throws<ArgumentException>(() => Product.Create(Guid.NewGuid(), Guid.Empty, "Emmental", 400, "g"));

    [Theory]
    [InlineData(400, "g", 5.95, 14.875, Dimension.Mass)]
    [InlineData(1, "kg", 1.49, 1.49, Dimension.Mass)]
    [InlineData(400, "ml", 1.28, 3.2, Dimension.Volume)]
    [InlineData(6, "pièce", 2.10, 0.35, Dimension.Count)]
    public void Every_price_comes_down_to_a_price_per_kilogram_litre_or_piece(
        decimal quantity, string unit, decimal price, decimal unitPrice, Dimension dimension)
    {
        var entry = Emmental(quantity, unit).RecordPrice(Guid.NewGuid(), price, Today, Now);
        Assert.Equal(unitPrice, entry.UnitPrice);
        Assert.Equal(dimension, entry.Basis.Dimension);
    }

    [Fact]
    public void A_price_keeps_the_package_contents_it_was_observed_with()
    {
        var product = Emmental();
        var entry = product.RecordPrice(Guid.NewGuid(), 5.95m, Today, Now);
        product.Update(product.FoodItemId, product.Name, 150, "g");
        Assert.Equal(400, entry.PackageQuantity);
        Assert.Equal(14.875m, entry.UnitPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(1.234)]
    public void A_price_is_a_positive_amount_in_cents(decimal price) =>
        Assert.Throws<ArgumentException>(() => Emmental().RecordPrice(Guid.NewGuid(), price, Today, Now));

    [Fact]
    public void A_price_is_never_dated_in_the_future_nor_recorded_on_an_archived_product()
    {
        var product = Emmental();
        Assert.Throws<ArgumentException>(() => product.RecordPrice(Guid.NewGuid(), 1, Today.AddDays(2), Now));
        product.Archive();
        Assert.Throws<ArgumentException>(() => product.RecordPrice(Guid.NewGuid(), 1, Today, Now));
    }

    [Fact]
    public void The_latest_price_of_each_store_wins()
    {
        var product = Emmental();
        var leclerc = Guid.NewGuid();
        var biocoop = Guid.NewGuid();
        product.RecordPrice(leclerc, 5.50m, Today.AddDays(-30), Now);
        var latest = product.RecordPrice(leclerc, 5.95m, Today, Now);
        var other = product.RecordPrice(biocoop, 7.10m, Today.AddDays(-3), Now);

        Assert.Equal([latest, other], product.LatestPricePerStore().OrderBy(p => p.Price));
        Assert.True(product.RemovePrice(latest.Id));
        Assert.Equal(5.50m, product.LatestPricePerStore().Single(p => p.StoreId == leclerc).Price);
        Assert.False(product.RemovePrice(Guid.NewGuid()));
    }
}
