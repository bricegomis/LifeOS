using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Products;
using LifeOS.Domain.Receipts;
using LifeOS.Domain.Stores;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public sealed record ImportReceiptRequest(string Json);
public sealed record ReceiptContextRequest(Guid? StoreId, string? NewStoreName, string Date);
public sealed record CorrectReceiptLineRequest(string Label, decimal? Total, decimal? Quantity, string? Unit,
    decimal? StatedUnitPrice, bool PriceMismatchConfirmed);
public sealed record ValidateReceiptLineRequest(Guid? ProductId, Guid? FoodItemId, string? NewFoodName,
    string? NewProductName, decimal? PackageQuantity, string? PackageUnit, bool PackageConfirmed, bool PiecesComparable,
    bool ConfirmPurchaseUnit = false);

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
                .Select(r => new { r.Id, r.StoreName, r.StoreId, r.Date, r.CreatedAt, TotalLines = r.Lines.Count,
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
        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var r = await db.Receipts.Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == household, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
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
            if (request.StoreId is { } existing)
            {
                if (!await db.Stores.AnyAsync(s => s.Id == existing && s.HouseholdId == household, ct))
                    throw new ArgumentException("Magasin indisponible dans votre foyer.");
                storeId = existing;
            }
            else
            {
                var store = Store.Create(household, request.NewStoreName ?? "", "", false, false);
                db.Stores.Add(store); storeId = store.Id;
            }
            receipt.SetContext(storeId, request.Date);
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
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(receipt);
        });
        g.MapPost("/{id:guid}/lines/{lineId:guid}/validate", Validate);
        return app;
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
        if (line.Status == "validated") return Results.Ok(receipt);
        if (receipt.StoreId is null || receipt.Date is null) throw new ArgumentException("Sélectionnez le magasin et confirmez la date du ticket.");
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
            new DateTimeOffset(receipt.Date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), line.Id);
        observation.SetBasis(line.Unit!, product.PackageQuantity, product.PackageUnit, product.PackageConfirmed, product.PiecesComparable, line.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Ok(receipt);
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
