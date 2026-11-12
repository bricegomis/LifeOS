using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Food.Receipts;
using LifeOS.Domain.Food.Receipts;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Food.Receipts;

/// <summary>
/// Receipt tools for the assistant (ADR 0011, § 4): the photo is taken in the app, the assistant
/// reads it and types the prices the user confirmed. The API reads nothing in the image.
/// </summary>
[McpServerToolType]
public sealed class ReceiptAssistantTools
{
    [McpServerTool(Name = "list_receipts", ReadOnly = true, Title = "Tickets")]
    [Description("Liste les tickets photographiés dans l'application, du plus récent au plus ancien. " +
        "status : pending (à traiter, par défaut), processed (traités) ou all.")]
    public static async Task<List<ReceiptDto>> ListReceipts(IHttpContextAccessor accessor, LifeOSDbContext db,
        string status = Receipt.Pending, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var query = db.Receipts.AsNoTracking().Where(r => r.HouseholdId == household.HouseholdId);
        query = status switch
        {
            Receipt.Pending or Receipt.Processed => query.Where(r => r.Status == status),
            "all" => query,
            _ => throw new ArgumentException("status : pending, processed ou all."),
        };
        return await query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReceiptDto(r.Id, r.StoreId, r.PurchaseDate, r.Status, r.CreatedAt)).ToListAsync(ct);
    }

    [McpServerTool(Name = "get_receipt_photo", ReadOnly = true, Title = "Photo d'un ticket")]
    [Description("Renvoie la photo d'un ticket pour la lire. Proposez ensuite pour chaque ligne le produit du foyer " +
        "correspondant (search_products), faites confirmer l'utilisateur, puis saisissez les prix avec add_price " +
        "et marquez le ticket traité avec update_receipt.")]
    public static async Task<CallToolResult> GetReceiptPhoto(IHttpContextAccessor accessor, LifeOSDbContext db,
        ReceiptPhotoStorage storage, Guid receiptId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var receipt = (await db.Receipts.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == receiptId && r.HouseholdId == household.HouseholdId, ct)).OrNotFound("Ticket");
        if (!storage.Exists(receipt.ImagePath)) throw AssistantRequest.NotFound("Photo du ticket");
        await using var file = storage.OpenRead(receipt.ImagePath);
        using var bytes = new MemoryStream();
        await file.CopyToAsync(bytes, ct);
        var context = $"Ticket {receipt.Id} ({receipt.Status}) ; magasin : {receipt.StoreId?.ToString() ?? "non renseigné"} ; " +
            $"date d'achat : {receipt.PurchaseDate?.ToString("yyyy-MM-dd") ?? "non renseignée"}.";
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = context }, ImageContentBlock.FromBytes(bytes.ToArray(), receipt.ImageContentType)],
        };
    }

    [McpServerTool(Name = "update_receipt", Idempotent = true, Title = "Mettre à jour un ticket")]
    [Description("Renseigne le magasin et la date d'achat d'un ticket et/ou le marque traité. Un champ omis reste inchangé.")]
    public static async Task<ReceiptDto> UpdateReceipt(IHttpContextAccessor accessor, LifeOSDbContext db,
        ReceiptUseCases receipts, Guid receiptId,
        [Description("Magasin du foyer (list_stores).")] Guid? storeId = null,
        [Description("Date d'achat (aaaa-MM-jj).")] DateOnly? purchaseDate = null,
        [Description("true : traité ; false : remis à traiter.")] bool? processed = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var current = (await db.Receipts.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == receiptId && r.HouseholdId == household.HouseholdId, ct)).OrNotFound("Ticket");
        var updated = await receipts.UpdateAsync(household.HouseholdId, receiptId, storeId ?? current.StoreId,
            purchaseDate ?? current.PurchaseDate, processed ?? current.Status == Receipt.Processed, ct);
        return ReceiptDto.From(updated.OrNotFound("Ticket"));
    }
}
