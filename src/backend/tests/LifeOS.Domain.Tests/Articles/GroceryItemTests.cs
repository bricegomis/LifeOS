using LifeOS.Domain.Products;

namespace LifeOS.Domain.Tests.Articles;

public class GroceryItemTests
{
    [Fact]
    public void Food_association_is_required_and_stable()
    {
        Assert.Throws<ArgumentException>(() => Product.Create(Guid.NewGuid(), Guid.Empty, "Lait", "L"));
        var product = Product.Create(Guid.NewGuid(), Guid.NewGuid(), "Lait", "L");
        Assert.Throws<ArgumentException>(() => product.LinkFood(Guid.NewGuid()));
        product.ObservePrice(Guid.NewGuid(), 1.2m, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => product.Update("Lait", "", "unit", null, null, null, null, false, false));
        product.Archive();
        Assert.Single(product.PriceHistory);
        Assert.Throws<ArgumentException>(() => product.ObservePrice(Guid.NewGuid(), 1m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Minimum_uses_latest_observation_per_product_and_store_before_comparing()
    {
        var household = Guid.NewGuid(); var food = Guid.NewGuid(); var store = Guid.NewGuid();
        var a = Product.Create(household, food, "A", "kg");
        var b = Product.Create(household, food, "B", "kg");
        a.ObservePrice(store, 1m, DateTimeOffset.UnixEpoch);
        a.ObservePrice(store, 5m, DateTimeOffset.UnixEpoch.AddDays(2));
        b.ObservePrice(store, 3m, DateTimeOffset.UnixEpoch.AddDays(1));
        var minimum = Assert.Single(ComparablePrices.Minimums([a, b]));
        Assert.Equal(3m, minimum.Price); Assert.Equal(b.Id, minimum.ProductId); Assert.Equal(store, minimum.StoreId);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(1), minimum.ObservedAt);
    }

    [Fact]
    public void Package_confirmation_is_snapshotted_and_incompatible_bases_are_separate()
    {
        var household = Guid.NewGuid(); var food = Guid.NewGuid(); var store = Guid.NewGuid();
        var cheese = Product.Create(household, food, "Emmental 400g", "bag");
        cheese.Update(cheese.Name, "", "bag", null, null, 400m, "g", false, false);
        cheese.ObservePrice(store, 5.95m, DateTimeOffset.UnixEpoch);
        Assert.Empty(ComparablePrices.Minimums([cheese]));
        cheese.Update(cheese.Name, "", "bag", null, null, 400m, "g", true, false);
        Assert.Empty(ComparablePrices.Minimums([cheese]));
        cheese.ObservePrice(store, 5.95m, DateTimeOffset.UnixEpoch.AddDays(1));
        var minimum = Assert.Single(ComparablePrices.Minimums([cheese]));
        Assert.Equal(14.875m, minimum.Price); Assert.Equal("kg", minimum.Unit);
        var liquid = Product.Create(household, food, "Autre base", "L");
        liquid.ObservePrice(store, 1m, DateTimeOffset.UtcNow);
        Assert.Equal(2, ComparablePrices.Minimums([cheese, liquid]).Count);
        var piece = Product.Create(household, food, "Pièce inconnue", "unit");
        piece.ObservePrice(store, 0m, DateTimeOffset.UtcNow);
        Assert.Empty(ComparablePrices.Minimums([piece]));
    }
}
