using LifeOS.Domain.Food.Receipts;

namespace LifeOS.Application.Food.Receipts;

/// <summary>A file written by the storage: server-generated name, checked type, SHA-256 of the bytes.</summary>
public sealed record StoredFile(string FileName, string ContentType, string Sha256);

/// <summary>Durable storage of receipt photos (volume <c>lifeos_receipts</c>).</summary>
public interface IReceiptPhotoFiles
{
    Task<StoredFile> SaveAsync(Stream content, long length, string? contentType, CancellationToken ct);
    void Delete(string fileName);
}

public interface IReceiptData
{
    Task<Receipt?> FindAsync(Guid householdId, Guid receiptId, CancellationToken ct);
    Task<bool> StoreBelongsToHouseholdAsync(Guid householdId, Guid storeId, CancellationToken ct);
    Task<bool> PhotoExistsAsync(Guid householdId, string sha256, CancellationToken ct);
    void Add(Receipt receipt);
    Task SaveAsync(CancellationToken ct);
}

public sealed record UploadReceiptResult(Receipt? Receipt)
{
    public bool IsDuplicate => Receipt is null;
}

/// <summary>
/// A receipt is a stored photo (ADR 0010): uploading creates no product and no price. Store and
/// purchase date are optional and belong to the household; the same photo is kept only once.
/// </summary>
public sealed class ReceiptUseCases(IReceiptData data, IReceiptPhotoFiles files, TimeProvider clock)
{
    public async Task<UploadReceiptResult> UploadAsync(Guid householdId, Stream photo, long length, string? contentType,
        Guid? storeId, DateOnly? purchaseDate, CancellationToken ct)
    {
        await RequireStore(householdId, storeId, ct);
        var stored = await files.SaveAsync(photo, length, contentType, ct);
        var kept = false;
        try
        {
            if (await data.PhotoExistsAsync(householdId, stored.Sha256, ct)) return new UploadReceiptResult(null);
            var receipt = Receipt.Create(householdId, stored.Sha256, stored.FileName, stored.ContentType,
                storeId, purchaseDate, clock.GetUtcNow());
            data.Add(receipt);
            await data.SaveAsync(ct);
            kept = true;
            return new UploadReceiptResult(receipt);
        }
        finally
        {
            if (!kept) files.Delete(stored.FileName);
        }
    }

    /// <returns>The updated receipt, or null when it is not in the household.</returns>
    public async Task<Receipt?> UpdateAsync(Guid householdId, Guid receiptId, Guid? storeId, DateOnly? purchaseDate,
        bool processed, CancellationToken ct)
    {
        var receipt = await data.FindAsync(householdId, receiptId, ct);
        if (receipt is null) return null;
        await RequireStore(householdId, storeId, ct);
        receipt.SetContext(storeId, purchaseDate, clock.GetUtcNow());
        if (processed) receipt.MarkProcessed(); else receipt.Reopen();
        await data.SaveAsync(ct);
        return receipt;
    }

    private async Task RequireStore(Guid householdId, Guid? storeId, CancellationToken ct)
    {
        if (storeId is { } id && !await data.StoreBelongsToHouseholdAsync(householdId, id, ct))
            throw new ArgumentException("Magasin indisponible dans votre foyer.");
    }
}
