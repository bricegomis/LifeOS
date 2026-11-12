using LifeOS.Domain.Food.Receipts;

namespace LifeOS.Domain.Tests.Receipts;

public sealed class ReceiptTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    private static Receipt NewReceipt(Guid? store = null, DateOnly? date = null) =>
        Receipt.Create(Guid.NewGuid(), Hash, "photo.jpg", "image/jpeg", store, date, Now);

    [Fact]
    public void A_receipt_is_a_pending_photo_and_needs_neither_store_nor_date()
    {
        var receipt = NewReceipt();
        Assert.Equal(Receipt.Pending, receipt.Status);
        Assert.Null(receipt.StoreId);
        Assert.Null(receipt.PurchaseDate);
        Assert.Equal(Now, receipt.CreatedAt);
        Assert.Equal(Hash.ToUpperInvariant(), receipt.DocumentHash);
    }

    [Fact]
    public void Store_and_date_can_be_added_or_cleared_later()
    {
        var receipt = NewReceipt();
        var store = Guid.NewGuid();
        receipt.SetContext(store, new DateOnly(2026, 11, 8), Now);
        Assert.Equal(store, receipt.StoreId);
        Assert.Equal(new DateOnly(2026, 11, 8), receipt.PurchaseDate);
        receipt.SetContext(null, null, Now);
        Assert.Null(receipt.StoreId);
        Assert.Null(receipt.PurchaseDate);
    }

    [Fact]
    public void Processing_is_reversible()
    {
        var receipt = NewReceipt();
        receipt.MarkProcessed();
        Assert.Equal(Receipt.Processed, receipt.Status);
        receipt.Reopen();
        Assert.Equal(Receipt.Pending, receipt.Status);
    }

    [Fact]
    public void A_purchase_date_in_the_future_is_rejected_with_one_day_of_time_zone_tolerance()
    {
        Assert.Equal(new DateOnly(2026, 11, 12), NewReceipt(date: new DateOnly(2026, 11, 12)).PurchaseDate);
        Assert.Throws<ArgumentException>(() => NewReceipt(date: new DateOnly(2026, 11, 13)));
    }

    [Theory]
    [InlineData("abc", "photo.jpg", "image/jpeg")]
    [InlineData(null, "", "image/jpeg")]
    [InlineData(null, "photo.gif", "image/gif")]
    public void Invalid_photo_references_are_rejected(string? hash, string path, string contentType) =>
        Assert.Throws<ArgumentException>(() =>
            Receipt.Create(Guid.NewGuid(), hash ?? Hash, path, contentType, null, null, Now));

    [Fact]
    public void An_empty_household_or_store_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Receipt.Create(Guid.Empty, Hash, "photo.jpg", "image/jpeg", null, null, Now));
        Assert.Throws<ArgumentException>(() => NewReceipt(store: Guid.Empty));
    }
}
