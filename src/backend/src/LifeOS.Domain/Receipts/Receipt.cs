using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
                receipt.Date = date;
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

    public void SetContext(Guid storeId, string date)
    {
        if (Lines.Any(l => l.Status == "validated")) throw new ArgumentException("Le contexte d'un ticket déjà validé est figé.");
        if (!DateOnly.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new ArgumentException("Date invalide : utilisez jj/MM/aaaa.");
        StoreId = storeId; Date = parsed;
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
