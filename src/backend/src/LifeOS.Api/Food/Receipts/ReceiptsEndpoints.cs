using System.Globalization;
using LifeOS.Api.Food.Files;
using LifeOS.Api.Households;
using LifeOS.Application.Food.Receipts;
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

/// <summary>Listing and serving are plain reads; upload and update go through <see cref="ReceiptUseCases"/>.</summary>
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
        ReceiptUseCases receipts, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        var photo = form.Files.GetFile("photo") ?? throw new ArgumentException("Sélectionnez une photo de ticket.");
        await using var content = photo.OpenReadStream();
        var result = await receipts.UploadAsync(household.HouseholdId, content, photo.Length, photo.ContentType,
            ParseStore(form["storeId"]), ParseDate(form["purchaseDate"]), ct);
        return result.Receipt is { } receipt
            ? Results.Created($"/api/receipts/{receipt.Id}", ReceiptDto.From(receipt))
            : Results.Problem("Cette photo est déjà enregistrée dans votre foyer.", statusCode: StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetPhoto(Guid id, HouseholdContext household, HttpContext context,
        LifeOSDbContext db, ReceiptPhotoStorage storage, CancellationToken ct)
    {
        var receipt = await db.Receipts.Where(r => r.Id == id && r.HouseholdId == household.HouseholdId)
            .Select(r => new { r.ImagePath, r.ImageContentType }).SingleOrDefaultAsync(ct);
        return storage.Serve(context, receipt?.ImagePath, receipt?.ImageContentType);
    }

    private static async Task<IResult> Update(Guid id, UpdateReceiptRequest request, HouseholdContext household,
        ReceiptUseCases receipts, CancellationToken ct) =>
        await receipts.UpdateAsync(household.HouseholdId, id, request.StoreId, request.PurchaseDate, request.Processed, ct) is { } receipt
            ? Results.Ok(ReceiptDto.From(receipt)) : Results.NotFound();

    private static Guid? ParseStore(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : Guid.TryParse(value, out var id) ? id
        : throw new ArgumentException("Magasin invalide.");

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date
        : throw new ArgumentException("Date d'achat invalide : utilisez aaaa-MM-jj.");
}
