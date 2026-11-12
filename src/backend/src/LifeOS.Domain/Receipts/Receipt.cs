using LifeOS.Domain.Common;

namespace LifeOS.Domain.Receipts;

/// <summary>
/// A stored photo of a till receipt (ADR 0010). It carries no lines and creates no product or
/// price: prices are typed by hand while looking at the photo, then the receipt is marked processed.
/// </summary>
public sealed class Receipt : Entity
{
    public const string Pending = "pending";
    public const string Processed = "processed";

    public Guid HouseholdId { get; private set; }
    public string DocumentHash { get; private set; } = "";
    public string ImagePath { get; private set; } = "";
    public string ImageContentType { get; private set; } = "";
    public Guid? StoreId { get; private set; }
    public DateOnly? PurchaseDate { get; private set; }
    public string Status { get; private set; } = Pending;
    public DateTimeOffset CreatedAt { get; private set; }

    private Receipt() { }

    public static Receipt Create(Guid householdId, string documentHash, string imagePath, string imageContentType,
        Guid? storeId, DateOnly? purchaseDate, DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer requis.");
        if (documentHash.Length != 64 || !documentHash.All(Uri.IsHexDigit)) throw new ArgumentException("Empreinte d'image invalide.");
        if (string.IsNullOrWhiteSpace(imagePath) || imagePath.Length > 500) throw new ArgumentException("Chemin de photo invalide.");
        if (imageContentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Format de photo invalide.");
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            DocumentHash = documentHash.ToUpperInvariant(),
            ImagePath = imagePath,
            ImageContentType = imageContentType,
            CreatedAt = now ?? DateTimeOffset.UtcNow,
        };
        receipt.SetContext(storeId, purchaseDate, now);
        return receipt;
    }

    /// <summary>Store and purchase date are both optional and can be filled in later.</summary>
    public void SetContext(Guid? storeId, DateOnly? purchaseDate, DateTimeOffset? now = null)
    {
        if (storeId == Guid.Empty) throw new ArgumentException("Magasin invalide.");
        var today = DateOnly.FromDateTime((now ?? DateTimeOffset.UtcNow).UtcDateTime);
        // One day of tolerance covers time zones ahead of UTC.
        if (purchaseDate > today.AddDays(1)) throw new ArgumentException("La date d'achat ne peut pas être dans le futur.");
        StoreId = storeId;
        PurchaseDate = purchaseDate;
    }

    public void MarkProcessed() => Status = Processed;

    public void Reopen() => Status = Pending;
}
