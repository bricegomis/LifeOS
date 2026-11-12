using LifeOS.Application.Food.Receipts;
using LifeOS.Domain.Food.Receipts;

namespace LifeOS.Application.Tests.Food;

public sealed class ReceiptUseCasesTests
{
    private sealed class FakeData : IReceiptData
    {
        public readonly List<Receipt> Receipts = [];
        public readonly List<(Guid Household, Guid Store)> Stores = [];
        public bool FailOnSave;

        public Task<Receipt?> FindAsync(Guid householdId, Guid receiptId, CancellationToken ct) =>
            Task.FromResult(Receipts.SingleOrDefault(r => r.Id == receiptId && r.HouseholdId == householdId));
        public Task<bool> StoreBelongsToHouseholdAsync(Guid householdId, Guid storeId, CancellationToken ct) =>
            Task.FromResult(Stores.Contains((householdId, storeId)));
        public Task<bool> PhotoExistsAsync(Guid householdId, string sha256, CancellationToken ct) =>
            Task.FromResult(Receipts.Any(r => r.HouseholdId == householdId && r.DocumentHash == sha256));
        public void Add(Receipt receipt) => Receipts.Add(receipt);
        public Task SaveAsync(CancellationToken ct) =>
            FailOnSave ? Task.FromException(new InvalidOperationException("database down")) : Task.CompletedTask;
    }

    private sealed class FakeFiles : IReceiptPhotoFiles
    {
        public readonly List<string> Files = [];
        public string Hash = new('A', 64);

        public Task<StoredFile> SaveAsync(Stream content, long length, string? contentType, CancellationToken ct)
        {
            var name = $"{Guid.NewGuid():N}.jpg";
            Files.Add(name);
            return Task.FromResult(new StoredFile(name, "image/jpeg", Hash));
        }

        public void Delete(string fileName) => Files.Remove(fileName);
    }

    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Store = Guid.NewGuid();

    private static (FakeData Data, FakeFiles Files, ReceiptUseCases UseCases) Setup()
    {
        var data = new FakeData();
        data.Stores.Add((Household, Store));
        var files = new FakeFiles();
        return (data, files, new ReceiptUseCases(data, files, FixedClock.Default));
    }

    private static Task<UploadReceiptResult> Upload(ReceiptUseCases useCases, Guid household, Guid? store = null, DateOnly? date = null) =>
        useCases.UploadAsync(household, new MemoryStream([1, 2, 3]), 3, "image/jpeg", store, date, default);

    [Fact]
    public async Task Uploading_keeps_the_photo_as_a_pending_receipt()
    {
        var (data, files, useCases) = Setup();
        var result = await Upload(useCases, Household, Store, FixedClock.Today);

        var receipt = Assert.IsType<Receipt>(result.Receipt);
        Assert.Equal(Receipt.Pending, receipt.Status);
        Assert.Equal(Store, receipt.StoreId);
        Assert.Equal(Assert.Single(files.Files), receipt.ImagePath);
        Assert.Same(receipt, Assert.Single(data.Receipts));
    }

    [Fact]
    public async Task The_same_photo_is_kept_once_per_household_and_the_copy_is_deleted()
    {
        var (_, files, useCases) = Setup();
        await Upload(useCases, Household);
        var duplicate = await Upload(useCases, Household);

        Assert.True(duplicate.IsDuplicate);
        Assert.Single(files.Files);
        Assert.False((await Upload(useCases, Guid.NewGuid())).IsDuplicate);
    }

    [Fact]
    public async Task A_store_of_another_household_is_rejected_before_any_file_is_written()
    {
        var (_, files, useCases) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => Upload(useCases, Household, Guid.NewGuid()));
        Assert.Empty(files.Files);
    }

    [Fact]
    public async Task The_file_is_deleted_when_saving_fails()
    {
        var (data, files, useCases) = Setup();
        data.FailOnSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Upload(useCases, Household));
        Assert.Empty(files.Files);
    }

    [Fact]
    public async Task Updating_sets_the_context_and_moves_the_receipt_in_and_out_of_the_inbox()
    {
        var (_, _, useCases) = Setup();
        var receipt = (await Upload(useCases, Household)).Receipt!;

        var processed = await useCases.UpdateAsync(Household, receipt.Id, Store, FixedClock.Today, true, default);
        Assert.Equal(Receipt.Processed, processed!.Status);
        Assert.Equal(FixedClock.Today, processed.PurchaseDate);

        var reopened = await useCases.UpdateAsync(Household, receipt.Id, null, null, false, default);
        Assert.Equal(Receipt.Pending, reopened!.Status);
        Assert.Null(reopened.StoreId);

        Assert.Null(await useCases.UpdateAsync(Guid.NewGuid(), receipt.Id, null, null, true, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCases.UpdateAsync(Household, receipt.Id, null, FixedClock.Today.AddDays(5), true, default));
    }
}
