using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeOS.Domain.Common;
using LifeOS.Domain.Products;

namespace LifeOS.Domain.Receipts;

public sealed class Receipt : Entity
{
    public Guid HouseholdId { get; private set; }
    public string RawDocument { get; private set; } = "";
    public string DocumentHash { get; private set; } = "";
    public string StoreName { get; private set; } = "";
    public Guid? StoreId { get; private set; }
    public DateOnly? Date { get; private set; }
    public DateTimeOffset? PurchasedAt { get; private set; }
    public string Status { get; private set; } = "draft";
    [JsonIgnore]
    public string? ImagePath { get; private set; }
    public bool HasPhoto => ImagePath is not null;
    public string? ImageContentType { get; private set; }
    public string? OcrText { get; private set; }
    public string? OcrError { get; private set; }
    public int? DeclaredLines { get; private set; }
    public int? DetectedLines { get; private set; }
    public string? Diagnostic { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<ReceiptLine> Lines { get; private set; } = [];
    public int CompletedLines => Lines.Count(l => l.Status != "pending");

    private Receipt() { }
    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    public static Receipt Import(Guid household, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || Encoding.UTF8.GetByteCount(raw) > 1_048_576)
            throw new ArgumentException("Le document JSON doit contenir entre 1 octet et 1 Mo.");
        JsonDocument doc;
        try { doc = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 32 }); }
        catch (JsonException e) { throw new ArgumentException("JSON illisible : aucun ticket importé.", e); }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("aliments", out var foods) || foods.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Structure attendue : objet JSON avec tableau aliments.");
            var receipt = new Receipt
            {
                Id = Guid.NewGuid(), HouseholdId = household, RawDocument = raw, DocumentHash = Hash(raw),
                StoreName = Text(root, "magasin") ?? "", CreatedAt = DateTimeOffset.UtcNow,
                DeclaredLines = Integer(root, "nombre_lignes_ticket"), DetectedLines = Integer(root, "nombre_lignes_detectees"),
            };
            if (DateOnly.TryParseExact(Text(root, "date"), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                receipt.Date = date;
                receipt.PurchasedAt = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            }
            var index = 0;
            foreach (var food in foods.EnumerateArray())
            {
                if (food.ValueKind != JsonValueKind.Object) throw new ArgumentException("Chaque élément aliments doit être un objet : import annulé.");
                receipt.Lines.Add(ReceiptLine.Import(receipt.Id, household, index++, food.GetRawText(),
                    Text(food, "ligne_ticket") ?? "", Text(food, "libelle") ?? "",
                    Number(food, "prix_achat"), Number(food, "quantite"), Unit(Text(food, "unite")),
                    Number(food, "prix_quantite"), false,
                    food.TryGetProperty("prix_quantite", out var stated) && stated.ValueKind != JsonValueKind.Null
                        && Number(food, "prix_quantite") is null ? "Prix unitaire source invalide : corrigez ou retirez-le explicitement." : null));
            }
            if (root.TryGetProperty("lignes_non_alimentaires_exclues", out var excluded))
            {
                if (excluded.ValueKind != JsonValueKind.Array) throw new ArgumentException("Mentions exclues : tableau attendu.");
                foreach (var mention in excluded.EnumerateArray())
                {
                    if (mention.ValueKind != JsonValueKind.String) throw new ArgumentException("Mentions exclues : textes attendus.");
                    receipt.Lines.Add(ReceiptLine.Import(receipt.Id, household, index++, mention.GetRawText(),
                        mention.GetString() ?? "", mention.GetString() ?? "", null, null, null, null, true));
                }
            }
            if (index is < 1 or > 500) throw new ArgumentException("Un ticket doit contenir entre 1 et 500 lignes.");
            var issues = new List<string>();
            if (receipt.Date is null) issues.Add("Date absente ou invalide : corrigez au format jj/MM/aaaa.");
            if (receipt.StoreName.Length > 200) issues.Add("Nom de magasin trop long : sélectionnez un magasin.");
            if (receipt.DeclaredLines != index || receipt.DetectedLines != foods.GetArrayLength())
                issues.Add("Les compteurs annoncés ne correspondent pas au contenu disponible.");
            if (receipt.Lines.Any(l => l.ExcludedMention))
                issues.Add("Mentions exclues conservées après les lignes alimentaires ; leur position dans le ticket original n'est pas fournie.");
            receipt.Diagnostic = issues.Count == 0 ? null : string.Join(" ", issues);
            return receipt;
        }
    }

    public static Receipt CreatePhotoDraft(Guid household, string documentHash, string imagePath,
        string imageContentType, string ocrText, string? ocrError, string storeName,
        DateTimeOffset? purchasedAt, IReadOnlyList<ReceiptOcrCandidate> candidates)
    {
        if (household == Guid.Empty) throw new ArgumentException("Foyer requis.");
        if (documentHash.Length != 64 || !documentHash.All(Uri.IsHexDigit)) throw new ArgumentException("Empreinte d'image invalide.");
        if (string.IsNullOrWhiteSpace(imagePath) || imagePath.Length > 500) throw new ArgumentException("Chemin de photo invalide.");
        if (imageContentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Format de photo invalide.");
        if (ocrText.Length > 100_000) throw new ArgumentException("Texte OCR trop volumineux.");
        if (ocrError?.Length > 1000) ocrError = ocrError[..1000];
        if (candidates.Count > 500) throw new ArgumentException("Un ticket ne peut pas contenir plus de 500 lignes OCR.");
        if (storeName.Length > 200) storeName = storeName[..200];

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            HouseholdId = household,
            DocumentHash = documentHash.ToUpperInvariant(),
            RawDocument = ocrText,
            OcrText = ocrText,
            OcrError = ocrError,
            ImagePath = imagePath,
            ImageContentType = imageContentType,
            StoreName = storeName,
            PurchasedAt = purchasedAt?.ToUniversalTime(),
            Date = purchasedAt is null ? null : DateOnly.FromDateTime(purchasedAt.Value.UtcDateTime),
            CreatedAt = DateTimeOffset.UtcNow,
            DeclaredLines = null,
            DetectedLines = candidates.Count,
            Diagnostic = ocrError ?? (candidates.Count == 0
                ? "Aucune ligne détectée : ajoutez les lignes manuellement avant validation."
                : "Les propositions OCR ne sont pas vérifiées : relisez et corrigez chaque ligne."),
        };

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            receipt.Lines.Add(ReceiptLine.Import(receipt.Id, household, index, candidate.SourceText,
                candidate.SourceText, candidate.Label, candidate.Total, candidate.Quantity, candidate.Unit,
                candidate.StatedUnitPrice, false, "Proposition OCR à vérifier avant validation."));
        }

        return receipt;
    }

    public void AddDraftLine(string label)
    {
        RequireDraft();
        if (Lines.Count >= 500) throw new ArgumentException("Un ticket ne peut pas contenir plus de 500 lignes.");
        var source = label?.Trim() ?? "";
        Lines.Add(ReceiptLine.Import(Id, HouseholdId, Lines.Count, source, source, source,
            null, null, null, null, false, "Ligne ajoutée manuellement : complétez tous les champs."));
    }

    public void SetContext(Guid storeId, string date)
    {
        RequireDraft();
        if (Lines.Any(l => l.Status == "validated")) throw new ArgumentException("Le contexte d'un ticket déjà validé est figé.");
        if (!DateOnly.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new ArgumentException("Date invalide : utilisez jj/MM/aaaa.");
        StoreId = storeId; Date = parsed;
        PurchasedAt = new DateTimeOffset(parsed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    public void SetPhotoContext(Guid storeId, string storeName, DateTimeOffset purchasedAt)
    {
        RequireDraft();
        if (Lines.Any(l => l.Status == "validated")) throw new ArgumentException("Le contexte d'un ticket déjà validé est figé.");
        if (string.IsNullOrWhiteSpace(storeName) || storeName.Length > 200) throw new ArgumentException("Nom du magasin requis (200 caractères maximum).");
        StoreId = storeId;
        StoreName = storeName.Trim();
        PurchasedAt = purchasedAt.ToUniversalTime();
        Date = DateOnly.FromDateTime(PurchasedAt.Value.UtcDateTime);
    }

    public void Complete()
    {
        RequireDraft();
        if (StoreId is null || PurchasedAt is null) throw new ArgumentException("Sélectionnez le magasin et confirmez la date du ticket.");
        if (Lines.Count == 0 || Lines.Any(l => l.Status == "pending"))
            throw new ArgumentException("Traitez ou ignorez chaque ligne avant de valider le ticket.");
        Status = "validated";
    }

    private void RequireDraft()
    {
        if (Status != "draft") throw new ArgumentException("Ce ticket est déjà validé et ne peut plus être modifié.");
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static decimal? Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n)
        && n is >= 0 and <= 1000000 && decimal.Round(n, 6) == n ? n : null;
    private static int? Integer(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;
    private static string? Unit(string? value) => value switch
    {
        "kg" => "kg", "g" => "g", "L" or "l" or "litre" => "L", "ml" => "ml",
        "unité" or "unit" or "unite" => "unit", "boîte" or "boite" => "box",
        "sachet" => "bag", "pot" => "pot", "paquet" => "pack", _ => null,
    };
}

public sealed record ReceiptOcrCandidate(string SourceText, string Label, decimal? Total,
    decimal? Quantity, string? Unit, decimal? StatedUnitPrice);

public sealed class ReceiptLine : Entity
{
    public Guid ReceiptId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public int Position { get; private set; }
    public string RawSource { get; private set; } = "";
    public string SourceText { get; private set; } = "";
    public bool ExcludedMention { get; private set; }
    public string Label { get; private set; } = "";
    public decimal? Total { get; private set; }
    public decimal? Quantity { get; private set; }
    public string? Unit { get; private set; }
    public decimal? StatedUnitPrice { get; private set; }
    public string? SourceDiagnostic { get; private set; }
    public string Status { get; private set; } = "pending";
    public Guid? ProductId { get; private set; }
    public bool PriceMismatchConfirmed { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IReadOnlyList<string> Diagnostics
    {
        get
        {
            var issues = new List<string>();
            if (SourceDiagnostic is not null) issues.Add(SourceDiagnostic);
            if (ExcludedMention) issues.Add("Mention non alimentaire : ignorez cette ligne sans création.");
            if (string.IsNullOrWhiteSpace(Label) || Label.Length > 500) issues.Add("Libellé requis (500 caractères maximum).");
            if (Total is null or < 0 or > 1000000 || Total is not null && decimal.Round(Total.Value, 2) != Total) issues.Add("Montant total requis, en euros et centimes.");
            if (Quantity is null or <= 0 or > 1000000 || Quantity is not null && decimal.Round(Quantity.Value, 6) != Quantity) issues.Add("Quantité positive requise (6 décimales maximum).");
            if (Unit is null) issues.Add("Unité d'achat requise.");
            if (Total is >= 0 && Quantity is > 0 && Total / Quantity >= 10000000000m) issues.Add("Prix normalisé hors limites : corrigez le montant ou la quantité.");
            if (StatedUnitPrice is < 0 or > 1000000 || StatedUnitPrice is not null && decimal.Round(StatedUnitPrice.Value, 6) != StatedUnitPrice) issues.Add("Prix unitaire invalide (6 décimales maximum).");
            if (HasPriceMismatch && !PriceMismatchConfirmed) issues.Add("Montant, quantité et prix unitaire incohérents : corrigez ou confirmez explicitement.");
            return issues;
        }
    }
    public bool HasPriceMismatch => Total is not null && Quantity is not null && StatedUnitPrice is not null
        && Math.Abs(Total.Value - Quantity.Value * StatedUnitPrice.Value) > 0.01m + Quantity.Value * 0.005m;

    private ReceiptLine() { }
    internal static ReceiptLine Import(Guid receipt, Guid household, int position, string raw, string text,
        string label, decimal? total, decimal? quantity, string? unit, decimal? statedPrice, bool excluded, string? sourceDiagnostic = null) =>
        new() { Id = Guid.NewGuid(), ReceiptId = receipt, HouseholdId = household, Position = position,
            RawSource = raw, SourceText = text, Label = label, Total = total, Quantity = quantity,
            Unit = unit, StatedUnitPrice = statedPrice, ExcludedMention = excluded, SourceDiagnostic = sourceDiagnostic };

    public void Correct(string label, decimal? total, decimal? quantity, string? unit, decimal? stated, bool confirmed)
    {
        RequirePending();
        if (unit is not null) PurchaseUnits.Validate(unit);
        Label = label; Total = total; Quantity = quantity; Unit = unit; StatedUnitPrice = stated;
        PriceMismatchConfirmed = confirmed;
        SourceDiagnostic = null;
    }
    public void Validate(Guid product)
    {
        RequirePending();
        if (Diagnostics.Count != 0) throw new ArgumentException(string.Join(" ", Diagnostics));
        ProductId = product; Status = "validated"; CompletedAt = DateTimeOffset.UtcNow;
    }
    public void Ignore() { RequirePending(); Status = "ignored"; CompletedAt = DateTimeOffset.UtcNow; }
    private void RequirePending() { if (Status != "pending") throw new ArgumentException("Cette ligne est déjà traitée et ne peut plus être modifiée."); }
}

public sealed class Purchase : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid ReceiptLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid StoreId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal Total { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "";
    private Purchase() { }
    public static Purchase FromLine(Receipt r, ReceiptLine l, Product p) =>
        new() { Id = Guid.NewGuid(), HouseholdId = r.HouseholdId, ReceiptLineId = l.Id, ProductId = p.Id,
            StoreId = r.StoreId!.Value, Date = r.Date!.Value, Total = l.Total!.Value, Quantity = l.Quantity!.Value, Unit = l.Unit! };
}
