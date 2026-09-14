using LifeOS.Domain.Articles;

namespace LifeOS.Domain.Tests.Articles;

public class GroceryItemTests
{
    [Fact]
    public void Create_rejects_empty_household_id()
    {
        Assert.Throws<ArgumentException>(() =>
            GroceryItem.Create(Guid.Empty, "Lait", "Lait demi-écrémé", GroceryItemUnit.Liter));
    }

    [Fact]
    public void AddPriceEntry_appends_to_history_and_bumps_updated_at()
    {
        var article = GroceryItem.Create(Guid.NewGuid(), "Lait", "Lait demi-écrémé", GroceryItemUnit.Liter, DateTimeOffset.UnixEpoch);
        var storeId = Guid.NewGuid();

        var entry = article.AddPriceEntry(storeId, 1.20m, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.Single(article.PriceHistory);
        Assert.Equal(entry.Id, article.PriceHistory[0].Id);
        Assert.Equal(storeId, entry.StoreId);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(1), article.UpdatedAt);
    }

    [Fact]
    public void AddPriceEntry_rejects_negative_price()
    {
        var article = GroceryItem.Create(Guid.NewGuid(), "Lait", "Lait demi-écrémé", GroceryItemUnit.Liter);

        Assert.Throws<ArgumentOutOfRangeException>(() => article.AddPriceEntry(Guid.NewGuid(), -1m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RemovePriceEntry_removes_matching_entry_only()
    {
        var article = GroceryItem.Create(Guid.NewGuid(), "Lait", "Lait demi-écrémé", GroceryItemUnit.Liter);
        var entry = article.AddPriceEntry(Guid.NewGuid(), 1.20m, DateTimeOffset.UtcNow);
        article.AddPriceEntry(Guid.NewGuid(), 1.30m, DateTimeOffset.UtcNow);

        var removed = article.RemovePriceEntry(entry.Id);

        Assert.True(removed);
        Assert.Single(article.PriceHistory);
        Assert.DoesNotContain(article.PriceHistory, e => e.Id == entry.Id);
    }

    [Fact]
    public void Rehydrate_restores_price_history()
    {
        var householdId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var now = DateTimeOffset.UnixEpoch;
        var priceEntry = GroceryPriceEntry.Create(Guid.NewGuid(), 2.5m, now, now);

        var article = GroceryItem.Rehydrate(
            articleId, householdId, "Lait", "Lait demi-écrémé", GroceryItemUnit.Liter, [priceEntry], now, now);

        Assert.Equal(articleId, article.Id);
        Assert.Equal(householdId, article.HouseholdId);
        Assert.Single(article.PriceHistory);
        Assert.Equal(priceEntry.Id, article.PriceHistory[0].Id);
    }
}
