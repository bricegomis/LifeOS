using System.Globalization;
using LifeOS.Api.Food.Files;
using LifeOS.Api.Households;
using LifeOS.Domain.Food.Receipts;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Receipts;

public sealed record ReceiptDto(Guid Id, Guid? StoreId, DateOnly? PurchaseDate, string Status, DateTimeOffset CreatedAt)
{
    public static ReceiptDto From(Receipt r) => new(r.Id, r.StoreId, r.PurchaseDate, r.Status, r.CreatedAt);
}

/// <summary>Store and purchase date are optional; <c>Processed</c> moves the receipt out of the inbox.</summary>
public sealed record UpdateReceiptRequest(Guid? StoreId, DateOnly? PurchaseDate, bool Processed);

public static class ReceiptsEndpoints
{
    public static IEndpointRouteBuilder MapReceiptsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/receipts").RequireAuthorization().WithTags("Tickets");
        g.MapGet("/", List);
        g.MapPost("/photo", UploadPhoto).WithUploadLimit();
        g.MapGet("/{id:guid}/photo", GetPhoto);
        g.MapPut("/{id:guid}", Update);
        return app;
    }

    private static async Task<IResult> List(HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Receipts.Where(r => r.HouseholdId == household.HouseholdId).OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReceiptDto(r.Id, r.StoreId, r.PurchaseDate, r.Status, r.CreatedAt)).ToListAsync(ct));

    private static async Task<IResult> UploadPhoto(HttpRequest request, HouseholdContext household,
        LifeOSDbContext db, ReceiptPhotoStorage storage, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        var photo = form.Files.GetFile("photo") ?? throw new ArgumentException("Sélectionnez une photo de ticket.");
        var storeId = ParseStore(form["storeId"]);
        var purchaseDate = ParseDate(form["purchaseDate"]);
        await RequireStore(db, household, storeId, ct);

        var stored = await storage.SaveUploadAsync(photo, ct);
        var created = false;
        try
        {
            if (await db.Receipts.AnyAsync(r => r.HouseholdId == household.HouseholdId && r.DocumentHash == stored.Sha256, ct))
                return Results.Conflict(new { Detail = "Cette photo est déjà enregistrée dans votre foyer." });
            var receipt = Receipt.Create(household.HouseholdId, stored.Sha256, stored.FileName, stored.ContentType, storeId, purchaseDate);
            db.Receipts.Add(receipt);
            await db.SaveChangesAsync(ct);
            created = true;
            return Results.Created($"/api/receipts/{receipt.Id}", ReceiptDto.From(receipt));
        }
        finally
        {
            if (!created) storage.Delete(stored.FileName);
        }
    }

    private static async Task<IResult> GetPhoto(Guid id, HouseholdContext household, HttpContext context,
        LifeOSDbContext db, ReceiptPhotoStorage storage, CancellationToken ct)
    {
        var receipt = await db.Receipts.Where(r => r.Id == id && r.HouseholdId == household.HouseholdId)
            .Select(r => new { r.ImagePath, r.ImageContentType }).SingleOrDefaultAsync(ct);
        return storage.Serve(context, receipt?.ImagePath, receipt?.ImageContentType);
    }

    private static async Task<IResult> Update(Guid id, UpdateReceiptRequest request, HouseholdContext household,
        LifeOSDbContext db, CancellationToken ct)
    {
        var receipt = await db.Receipts.SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == household.HouseholdId, ct);
        if (receipt is null) return Results.NotFound();
        await RequireStore(db, household, request.StoreId, ct);
        receipt.SetContext(request.StoreId, request.PurchaseDate);
        if (request.Processed) receipt.MarkProcessed(); else receipt.Reopen();
        await db.SaveChangesAsync(ct);
        return Results.Ok(ReceiptDto.From(receipt));
    }

    private static async Task RequireStore(LifeOSDbContext db, HouseholdContext household, Guid? storeId, CancellationToken ct)
    {
        if (storeId is { } id && !await db.Stores.AnyAsync(s => s.Id == id && s.HouseholdId == household.HouseholdId, ct))
            throw new ArgumentException("Magasin indisponible dans votre foyer.");
    }

    private static Guid? ParseStore(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : Guid.TryParse(value, out var id) ? id
        : throw new ArgumentException("Magasin invalide.");

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date
        : throw new ArgumentException("Date d'achat invalide : utilisez aaaa-MM-jj.");
}
