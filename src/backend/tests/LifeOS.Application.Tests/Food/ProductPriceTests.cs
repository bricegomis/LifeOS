using LifeOS.Application.Food.Products;
using LifeOS.Domain.Food.Products;

namespace LifeOS.Application.Tests.Food;

public sealed class ProductPriceTests
{
    private sealed class FakeData : IProductPriceData
    {
        public readonly List<Product> Products = [];
        public readonly List<(Guid Household, Guid Store)> Stores = [];
        public readonly List<ProductPrice> Added = [];
        public readonly List<ProductPrice> Removed = [];
        public int Saves;

        public Task<Product?> FindProductAsync(Guid householdId, Guid productId, CancellationToken ct) =>
            Task.FromResult(Products.SingleOrDefault(p => p.Id == productId && p.HouseholdId == householdId));
        public Task<bool> StoreBelongsToHouseholdAsync(Guid householdId, Guid storeId, CancellationToken ct) =>
            Task.FromResult(Stores.Contains((householdId, storeId)));
        public void AddPrice(ProductPrice price) => Added.Add(price);
        public void RemovePrice(ProductPrice price) => Removed.Add(price);
        public Task SaveAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    }

    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Store = Guid.NewGuid();

    private static (FakeData Data, Product Product) Setup()
    {
        var data = new FakeData();
        var product = Product.Create(Household, Guid.NewGuid(), "Lait de coco", 400, "ml");
        data.Products.Add(product);
        data.Stores.Add((Household, Store));
        return (data, product);
    }

    [Fact]
    public async Task Recording_a_price_adds_it_with_the_store_and_saves()
    {
        var (data, product) = Setup();
        var result = await new RecordProductPrice(data, FixedClock.Default)
            .ExecuteAsync(Household, product.Id, Store, 1.28m, FixedClock.Today, default);

        Assert.Same(product, result);
        var price = Assert.Single(data.Added);
        Assert.Equal(Store, price.StoreId);
        Assert.Equal(3.2m, price.UnitPrice);
        Assert.Equal(1, data.Saves);
    }

    [Fact]
    public async Task A_store_of_another_household_is_rejected_and_nothing_is_saved()
    {
        var (data, product) = Setup();
        var foreignStore = Guid.NewGuid();
        data.Stores.Add((Guid.NewGuid(), foreignStore));
        await Assert.ThrowsAsync<ArgumentException>(() => new RecordProductPrice(data, FixedClock.Default)
            .ExecuteAsync(Household, product.Id, foreignStore, 1.28m, FixedClock.Today, default));
        Assert.Empty(data.Added);
        Assert.Equal(0, data.Saves);
    }

    [Fact]
    public async Task A_product_of_another_household_is_not_found()
    {
        var (data, product) = Setup();
        Assert.Null(await new RecordProductPrice(data, FixedClock.Default)
            .ExecuteAsync(Guid.NewGuid(), product.Id, Store, 1.28m, FixedClock.Today, default));
        Assert.Null(await new RemoveProductPrice(data).ExecuteAsync(Guid.NewGuid(), product.Id, Guid.NewGuid(), default));
        Assert.Equal(0, data.Saves);
    }

    [Fact]
    public async Task Removing_a_price_drops_only_that_price()
    {
        var (data, product) = Setup();
        var kept = product.RecordPrice(Store, 1.28m, FixedClock.Today.AddDays(-7));
        var mistyped = product.RecordPrice(Store, 12.80m, FixedClock.Today);

        Assert.Same(product, await new RemoveProductPrice(data).ExecuteAsync(Household, product.Id, mistyped.Id, default));
        Assert.Equal([kept], product.Prices);
        Assert.Equal([mistyped], data.Removed);
        Assert.Null(await new RemoveProductPrice(data).ExecuteAsync(Household, product.Id, mistyped.Id, default));
    }
}
