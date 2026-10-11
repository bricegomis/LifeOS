using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Authentication;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
using LifeOS.Domain.Products;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public sealed record ProductRequest(Guid FoodItemId, [property: Required, StringLength(500)] string Name,
    string Description, string PurchaseUnit, string? Brand, string? Barcode,
    decimal? PackageQuantity, string? PackageUnit, bool PackageConfirmed, bool PiecesComparable);
public sealed record PriceRequest(Guid StoreId, decimal Price, DateTimeOffset ObservedAt);

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/products").RequireAuthorization().WithTags("Produits").AddRequestValidation();
        g.MapGet("/", async (ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            return Results.Ok(await db.Products.Where(p => p.HouseholdId == household).OrderBy(p => p.Name).ToListAsync(ct));
        });
        g.MapPost("/", async (ProductRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            await RequireFood(db, household, request.FoodItemId, ct);
            var product = Product.Create(household, request.FoodItemId, request.Name, request.PurchaseUnit);
            Apply(product, request);
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/products/{product.Id}", product);
        });
        g.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });
        g.MapPut("/{id:guid}", async (Guid id, ProductRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household, ct);
            if (product is null) return Results.NotFound();
            await RequireFood(db, household, request.FoodItemId, ct);
            product.LinkFood(request.FoodItemId);
            Apply(product, request);
            await db.SaveChangesAsync(ct);
            return Results.Ok(product);
        });
        g.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household, ct);
            if (product is null) return Results.NotFound();
            product.Archive(); await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:guid}/price-entries", async (Guid id, PriceRequest request, ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household, ct);
            if (product is null) return Results.NotFound();
            if (!await db.Stores.AnyAsync(s => s.Id == request.StoreId && s.HouseholdId == household, ct))
                throw new ArgumentException("Magasin indisponible dans votre foyer.");
            product.ObservePrice(request.StoreId, request.Price, request.ObservedAt);
            await db.SaveChangesAsync(ct);
            return Results.Ok(product);
        });
        app.MapGet("/api/foods/prices", async (ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var products = await db.Products.Where(p => p.HouseholdId == household && p.FoodItemId != null).ToListAsync(ct);
            return Results.Ok(products.GroupBy(p => p.FoodItemId!.Value)
                .Select(g => new { FoodItemId = g.Key, Prices = ComparablePrices.Minimums(g) }));
        }).RequireAuthorization();
        return app;
    }

    internal static Task RequireFood(LifeOSDbContext db, Guid household, Guid id, CancellationToken ct) =>
        RequireFoodCore(db, household, id, ct);

    private static async Task RequireFoodCore(LifeOSDbContext db, Guid household, Guid id, CancellationToken ct)
    {
        if (db.FoodItems.Local.Any(f => f.Id == id && f.HouseholdId == household && !f.IsArchived)) return;
        if (!await db.FoodItems.AnyAsync(f => f.Id == id && f.HouseholdId == household && !f.IsArchived, ct))
            throw new ArgumentException("Choisissez un aliment actif de votre foyer.");
    }

    private static void Apply(Product p, ProductRequest r) => p.Update(r.Name, r.Description ?? "", r.PurchaseUnit,
        r.Brand, r.Barcode, r.PackageQuantity, r.PackageUnit, r.PackageConfirmed, r.PiecesComparable);
}
