using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Food.Products;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Products;

public sealed record ProductRequest(Guid FoodItemId, [property: Required, StringLength(500)] string Name,
    string Description, string PurchaseUnit, string? Brand, string? Barcode,
    decimal? PackageQuantity, string? PackageUnit, bool PackageConfirmed, bool PiecesComparable);
public sealed record PriceRequest(Guid StoreId, decimal Price, DateTimeOffset ObservedAt);

/// <summary>Simple management screen (ADR 0009): direct DbContext access, rules in <see cref="Product"/>.</summary>
public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/products").RequireAuthorization().WithTags("Produits").AddRequestValidation();
        g.MapGet("/", List);
        g.MapPost("/", Create);
        g.MapGet("/{id:guid}", Get);
        g.MapPut("/{id:guid}", Update);
        g.MapDelete("/{id:guid}", Archive);
        g.MapPost("/{id:guid}/price-entries", ObservePrice);
        app.MapGet("/api/foods/prices", ComparablePricesByFood).RequireAuthorization().WithTags("Produits");
        return app;
    }

    private static Task<Product?> Find(LifeOSDbContext db, HouseholdContext household, Guid id, CancellationToken ct) =>
        db.Products.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household.HouseholdId, ct);

    private static async Task<IResult> List(HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Products.Where(p => p.HouseholdId == household.HouseholdId).OrderBy(p => p.Name).ToListAsync(ct));

    private static async Task<IResult> Create(ProductRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        await RequireActiveFood(db, household, request.FoodItemId, ct);
        var product = Product.Create(household.HouseholdId, request.FoodItemId, request.Name, request.PurchaseUnit);
        Apply(product, request);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/products/{product.Id}", product);
    }

    private static async Task<IResult> Get(Guid id, HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
        await Find(db, household, id, ct) is { } product ? Results.Ok(product) : Results.NotFound();

    private static async Task<IResult> Update(Guid id, ProductRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var product = await Find(db, household, id, ct);
        if (product is null) return Results.NotFound();
        await RequireActiveFood(db, household, request.FoodItemId, ct);
        product.LinkFood(request.FoodItemId);
        Apply(product, request);
        await db.SaveChangesAsync(ct);
        return Results.Ok(product);
    }

    private static async Task<IResult> Archive(Guid id, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var product = await Find(db, household, id, ct);
        if (product is null) return Results.NotFound();
        product.Archive();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ObservePrice(Guid id, PriceRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var product = await Find(db, household, id, ct);
        if (product is null) return Results.NotFound();
        if (!await db.Stores.AnyAsync(s => s.Id == request.StoreId && s.HouseholdId == household.HouseholdId, ct))
            throw new ArgumentException("Magasin indisponible dans votre foyer.");
        product.ObservePrice(request.StoreId, request.Price, request.ObservedAt);
        await db.SaveChangesAsync(ct);
        return Results.Ok(product);
    }

    private static async Task<IResult> ComparablePricesByFood(HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.HouseholdId == household.HouseholdId && p.FoodItemId != null).ToListAsync(ct);
        return Results.Ok(products.GroupBy(p => p.FoodItemId!.Value)
            .Select(g => new { FoodItemId = g.Key, Prices = ComparablePrices.Minimums(g) }));
    }

    private static async Task RequireActiveFood(LifeOSDbContext db, HouseholdContext household, Guid id, CancellationToken ct)
    {
        if (!await db.FoodItems.AnyAsync(f => f.Id == id && f.HouseholdId == household.HouseholdId && !f.IsArchived, ct))
            throw new ArgumentException("Choisissez un aliment actif de votre foyer.");
    }

    private static void Apply(Product p, ProductRequest r) => p.Update(r.Name, r.Description ?? "", r.PurchaseUnit,
        r.Brand, r.Barcode, r.PackageQuantity, r.PackageUnit, r.PackageConfirmed, r.PiecesComparable);
}
