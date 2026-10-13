using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Receipts;
using LifeOS.Application.Households;
using LifeOS.Application.Receipts;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Products;
using LifeOS.Domain.Receipts;
using LifeOS.Domain.Stores;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.Api.Endpoints;

public sealed record ImportReceiptRequest(string Json);
public sealed record ReceiptContextRequest(Guid? StoreId, string? NewStoreName, string Date, DateTimeOffset? PurchasedAt = null);
public sealed record CorrectReceiptLineRequest(string Label, decimal? Total, decimal? Quantity, string? Unit,
    decimal? StatedUnitPrice, bool PriceMismatchConfirmed);
public sealed record AddReceiptLineRequest(string Label);
public sealed record ValidateReceiptLineRequest(Guid? ProductId, Guid? FoodItemId, string? NewFoodName,
    string? NewProductName, decimal? PackageQuantity, string? PackageUnit, bool PackageConfirmed, bool PiecesComparable,
    bool ConfirmPurchaseUnit = false);
public sealed record FinalizeReceiptLineRequest(Guid LineId, ValidateReceiptLineRequest Validation);
public sealed record FinalizeReceiptRequest(IReadOnlyList<FinalizeReceiptLineRequest> Lines);

public static class ReceiptsEndpoints
{
    public static IEndpointRouteBuilder MapReceiptsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/receipts").RequireAuthorization().WithTags("Tickets");
        g.MapGet("/", async (ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            return Results.Ok(await db.Receipts.Where(r => r.HouseholdId == household).OrderByDescending(r => r.CreatedAt)
                .Select(r => new { r.Id, r.StoreName, r.StoreId, r.Date, r.PurchasedAt, r.Status,
                    HasPhoto = r.ImagePath != null, r.CreatedAt, TotalLines = r.Lines.Count,
                    CompletedLines = r.Lines.Count(l => l.Status != "pending") }).ToListAsync(ct));
        });
        g.MapPost("/", async (ImportReceiptRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var receipt = Receipt.Import(household, request.Json);
            var existing = await db.Receipts.Where(r => r.HouseholdId == household && r.DocumentHash == receipt.DocumentHash)
                .Select(r => (Guid?)r.Id).SingleOrDefaultAsync(ct);
            if (existing is not null) return Results.Conflict(new { Detail = "Ce document est déjà importé dans votre foyer.", ReceiptId = existing });
            db.Receipts.Add(receipt);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/receipts/{receipt.Id}", receipt);
        });
        g.MapPost("/photo", UploadPhoto)
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024),
                new RequestFormLimitsAttribute { MultipartBodyLengthLimit = 11 * 1024 * 1024 });
        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var r = await db.Receipts.Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == household, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        });
        g.MapGet("/{id:guid}/photo", async (Guid id, ClaimsPrincipal user, HttpContext context,
            ResolveHouseholdForUserQuery households, LifeOSDbContext db, ReceiptPhotoStorage storage, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var receipt = await db.Receipts.Where(r => r.Id == id && r.HouseholdId == household)
                .Select(r => new { r.ImagePath, r.ImageContentType }).SingleOrDefaultAsync(ct);
            if (receipt?.ImagePath is null || receipt.ImageContentType is null) return Results.NotFound();
            var path = storage.ResolvePath(receipt.ImagePath);
            if (!File.Exists(path)) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(storage.OpenRead(receipt.ImagePath), receipt.ImageContentType);
        });
        g.MapPut("/{id:guid}/context", async (Guid id, ReceiptContextRequest request, ClaimsPrincipal user,
            ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var receipt = await LockedReceipt(db, household, id, ct);
            if (receipt is null) return Results.NotFound();
            Guid storeId;
            string storeName;
            if (request.StoreId is { } existing)
            {
                var store = await db.Stores.SingleOrDefaultAsync(s => s.Id == existing && s.HouseholdId == household, ct)
                    ?? throw new ArgumentException("Magasin indisponible dans votre foyer.");
                storeId = store.Id;
                storeName = store.Name;
            }
            else
            {
                var store = Store.Create(household, request.NewStoreName ?? "", "", false, false);
                db.Stores.Add(store); storeId = store.Id; storeName = store.Name;
            }
            if (request.PurchasedAt is { } purchasedAt)
                receipt.SetPhotoContext(storeId, storeName, purchasedAt);
            else
                receipt.SetContext(storeId, request.Date);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(receipt);
        });
        g.MapPost("/{id:guid}/lines", async (Guid id, AddReceiptLineRequest request, ClaimsPrincipal user,
            ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var receipt = await LockedReceipt(db, household, id, ct);
            if (receipt is null) return Results.NotFound();
            receipt.AddDraftLine(request.Label);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(receipt);
        });
        g.MapPut("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, CorrectReceiptLineRequest request,
            ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var receipt = await LockedReceipt(db, household, id, ct);
            var line = receipt?.Lines.SingleOrDefault(l => l.Id == lineId);
            if (line is null) return Results.NotFound();
            line.Correct(request.Label, request.Total, request.Quantity, request.Unit, request.StatedUnitPrice, request.PriceMismatchConfirmed);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(receipt);
        });
        g.MapPost("/{id:guid}/lines/{lineId:guid}/ignore", async (Guid id, Guid lineId,
            ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var receipt = await LockedReceipt(db, household, id, ct);
            var line = receipt?.Lines.SingleOrDefault(l => l.Id == lineId);
            if (line is null) return Results.NotFound();
            if (line.Status != "ignored") line.Ignore();
            if (receipt!.Status == "draft" && receipt.ImagePath is null && receipt.StoreId is not null
                && receipt.PurchasedAt is not null && receipt.Lines.Count > 0
                && receipt.Lines.All(l => l.Status != "pending"))
                receipt.Complete();
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(receipt);
        });
        g.MapPost("/{id:guid}/lines/{lineId:guid}/validate", Validate);
        g.MapPost("/{id:guid}/finalize", FinalizeReceipt);
        return app;
    }

    private static async Task<IResult> UploadPhoto(HttpRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, ReceiptPhotoStorage storage,
        IReceiptOcrService ocr, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(sub, ct);
        var form = await request.ReadFormAsync(ct);
        var photo = form.Files.GetFile("photo");
        if (photo is null) throw new ArgumentException("Sélectionnez une photo de ticket.");

        var stored = await storage.SaveAsync(photo, ct);
        var created = false;
        try
        {
            if (await db.Receipts.AnyAsync(r => r.HouseholdId == household && r.DocumentHash == stored.Sha256, ct))
                return Results.Conflict(new { Detail = "Cette photo est déjà importée dans votre foyer." });

            string text;
            string? ocrError = null;
            try
            {
                text = await ocr.ReadTextAsync(storage.ResolvePath(stored.FileName), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (IOException exception)
            {
                loggerFactory.CreateLogger("ReceiptPhotoUpload").LogWarning(exception, "Receipt OCR failed");
                text = "";
                ocrError = exception.Message;
            }

            var parsed = ReceiptOcrParser.Parse(text);
            var receipt = Receipt.CreatePhotoDraft(household, stored.Sha256, stored.FileName,
                stored.ContentType, text, ocrError, parsed.StoreName, parsed.PurchasedAt, parsed.Lines);
            db.Receipts.Add(receipt);
            await db.SaveChangesAsync(ct);
            created = true;
            return Results.Created($"/api/receipts/{receipt.Id}", receipt);
        }
        finally
        {
            if (!created) storage.Delete(stored.FileName);
        }
    }

    // Lock the document before loading its lines: concurrent requests observe the committed state.
    private static async Task<Receipt?> LockedReceipt(LifeOSDbContext db, Guid household, Guid id, CancellationToken ct)
    {
        var receipt = await db.Receipts.FromSqlInterpolated(
            $"""SELECT * FROM receipts WHERE "Id" = {id} AND "HouseholdId" = {household} FOR UPDATE""")
            .SingleOrDefaultAsync(ct);
        if (receipt is not null) await db.Entry(receipt).Collection(r => r.Lines).LoadAsync(ct);
        return receipt;
    }

    private static async Task<IResult> Validate(Guid id, Guid lineId, ValidateReceiptLineRequest request,
        ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(sub, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var receipt = await LockedReceipt(db, household, id, ct);
        var line = receipt?.Lines.SingleOrDefault(l => l.Id == lineId);
        if (receipt is null || line is null) return Results.NotFound();
        if (receipt.ImagePath is not null)
            throw new ArgumentException("Un ticket photo doit être validé en une seule opération depuis son brouillon.");
        if (line.Status == "validated") return Results.Ok(receipt);
        await ValidateLine(receipt, line, request, household, db, ct);
        if (receipt.Lines.All(l => l.Status != "pending")) receipt.Complete();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Ok(receipt);
    }

    private static async Task<IResult> FinalizeReceipt(Guid id, FinalizeReceiptRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(sub, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var receipt = await LockedReceipt(db, household, id, ct);
        if (receipt is null) return Results.NotFound();
        if (receipt.Status == "validated") return Results.Ok(receipt);
        if (receipt.StoreId is null || receipt.PurchasedAt is null)
            throw new ArgumentException("Sélectionnez le magasin et confirmez la date du ticket.");

        var pending = receipt.Lines.Where(line => line.Status == "pending").ToDictionary(line => line.Id);
        if (request.Lines is null || request.Lines.Count != pending.Count || request.Lines.Select(line => line.LineId).Distinct().Count() != request.Lines.Count
            || request.Lines.Any(line => !pending.ContainsKey(line.LineId)))
            throw new ArgumentException("La validation doit inclure exactement chaque ligne encore en attente.");

        foreach (var entry in request.Lines)
            await ValidateLine(receipt, pending[entry.LineId], entry.Validation, household, db, ct);
        receipt.Complete();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(receipt);
    }

    private static async Task ValidateLine(Receipt receipt, ReceiptLine line, ValidateReceiptLineRequest request,
        Guid household, LifeOSDbContext db, CancellationToken ct)
    {
        if (receipt.StoreId is null || receipt.Date is null)
            throw new ArgumentException("Sélectionnez le magasin et confirmez la date du ticket.");
        if (line.Diagnostics.Count != 0) throw new ArgumentException(string.Join(" ", line.Diagnostics));
        Product product;
        if (request.ProductId is { } productId)
        {
            product = await db.Products.FromSqlInterpolated(
                    $"""SELECT * FROM products WHERE id = {productId} AND household_id = {household} FOR UPDATE""")
                .SingleOrDefaultAsync(ct) ?? throw new ArgumentException("Produit indisponible dans votre foyer.");
            await db.Entry(product).Collection(p => p.PriceHistory).LoadAsync(ct);
            if (product.IsArchived) throw new ArgumentException("Produit archivé.");
            if (!product.PurchaseUnitConfirmed)
            {
                if (!request.ConfirmPurchaseUnit) throw new ArgumentException("Confirmez explicitement l'unité d'achat de ce produit historique.");
                product.Update(product.Name, product.Description, line.Unit!, product.Brand, product.Barcode,
                    product.PackageQuantity, product.PackageUnit, product.PackageConfirmed, product.PiecesComparable);
            }
            if (line.Unit != product.PurchaseUnit
                && !(line.Unit is "g" or "kg" && product.PurchaseUnit is "g" or "kg")
                && !(line.Unit is "ml" or "L" && product.PurchaseUnit is "ml" or "L"))
                throw new ArgumentException("L'unité de la ligne ne correspond pas à ce produit. Corrigez-la explicitement.");
            if (product.FoodItemId is null)
                product.LinkFood(await ResolveFood(db, household, request, ct));
            await ProductsEndpoints.RequireFood(db, household, product.FoodItemId!.Value, ct);
        }
        else
        {
            var foodId = await ResolveFood(db, household, request, ct);
            product = Product.Create(household, foodId, request.NewProductName ?? "", line.Unit!);
            product.Update(product.Name, "", line.Unit!, null, null, request.PackageQuantity, request.PackageUnit,
                request.PackageConfirmed, request.PiecesComparable);
            db.Products.Add(product);
        }
        line.Validate(product.Id);
        db.Purchases.Add(Purchase.FromLine(receipt, line, product));
        var observation = product.ObservePrice(receipt.StoreId.Value, line.Total!.Value / line.Quantity!.Value,
            receipt.PurchasedAt ?? new DateTimeOffset(receipt.Date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), line.Id);
        observation.SetBasis(line.Unit!, product.PackageQuantity, product.PackageUnit, product.PackageConfirmed, product.PiecesComparable, line.Id);
    }

    private static async Task<Guid> ResolveFood(LifeOSDbContext db, Guid household, ValidateReceiptLineRequest request, CancellationToken ct)
    {
        if (request.FoodItemId is { } id)
        {
            await ProductsEndpoints.RequireFood(db, household, id, ct); return id;
        }
        var food = FoodItem.CreateManual(household, request.NewFoodName ?? "", "");
        db.FoodItems.Add(food);
        // No request field carries nutrition or digestive information into this path.
        return food.Id;
    }
}
