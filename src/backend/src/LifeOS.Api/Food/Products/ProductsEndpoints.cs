using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Food.Products;
using LifeOS.Domain.Food;
using LifeOS.Domain.Food.Products;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Products;

public sealed record ProductRequest(
    Guid FoodItemId,
    [property: Required, StringLength(Product.MaxNameLength)] string Name,
    decimal PackageQuantity,
    [property: Required] string PackageUnit,
    [property: StringLength(100)] string? Brand = null,
    [property: StringLength(50)] string? Barcode = null);

public sealed record PriceRequest(Guid StoreId, decimal Price, DateOnly ObservedOn);

/// <summary>A price with its comparison base: price per kilogram, litre or piece.</summary>
public sealed record ProductPriceDto(Guid Id, Guid StoreId, decimal Price, DateOnly ObservedOn,
    decimal PackageQuantity, string PackageUnit, decimal UnitPrice, string UnitLabel)
{
    public static ProductPriceDto From(ProductPrice p) => new(p.Id, p.StoreId, p.Price, p.ObservedOn,
        p.PackageQuantity, p.PackageUnit, decimal.Round(p.UnitPrice, 4), Measures.BaseUnitLabel(p.Basis.Dimension));
}

/// <summary><c>LatestPrices</c> holds the most recent price of each store; <c>Prices</c> the whole history.</summary>
public sealed record ProductDto(Guid Id, Guid FoodItemId, string Name, string? Brand, string? Barcode,
    decimal PackageQuantity, string PackageUnit, bool IsArchived,
    List<ProductPriceDto> LatestPrices, List<ProductPriceDto> Prices, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static ProductDto From(Product p) => new(p.Id, p.FoodItemId, p.Name, p.Brand, p.Barcode,
        p.PackageQuantity, p.PackageUnit, p.IsArchived,
        p.LatestPricePerStore().Select(ProductPriceDto.From).ToList(),
        p.Prices.OrderByDescending(x => x.ObservedOn).ThenByDescending(x => x.CreatedAt).Select(ProductPriceDto.From).ToList(),
        p.CreatedAt, p.UpdatedAt);
}

/// <summary>
/// Product management is plain CRUD on the Food tables; recording and removing a price are use
/// cases (<see cref="RecordProductPrice"/>), since they check the store and freeze the package.
/// </summary>
public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/products").RequireAuthorization().WithTags("Produits").AddRequestValidation();
        g.MapGet("/", List).Produces<List<ProductDto>>();
        g.MapPost("/", Create).Produces<ProductDto>(StatusCodes.Status201Created);
        g.MapGet("/{id:guid}", Get).Produces<ProductDto>();
        g.MapPut("/{id:guid}", Update).Produces<ProductDto>();
        g.MapDelete("/{id:guid}", Archive);
        g.MapPost("/{id:guid}/prices", RecordPrice).Produces<ProductDto>();
        g.MapDelete("/{id:guid}/prices/{priceId:guid}", RemovePrice).Produces<ProductDto>();
        return app;
    }

    /// <param name="foodId">Optional: only the products of this food (food sheet).</param>
    private static async Task<IResult> List(HouseholdContext household, ProductLibrary products, CancellationToken ct, Guid? foodId = null) =>
        Results.Ok((await products.ListAsync(household.HouseholdId, foodId, ct: ct)).Select(ProductDto.From).ToList());

    private static async Task<IResult> Create(ProductRequest r, HouseholdContext household, ProductLibrary products, CancellationToken ct)
    {
        var product = await products.CreateAsync(household.HouseholdId, r, ct);
        return Results.Created($"/api/products/{product.Id}", ProductDto.From(product));
    }

    private static async Task<IResult> Get(Guid id, HouseholdContext household, ProductLibrary products, CancellationToken ct) =>
        await products.FindAsync(household.HouseholdId, id, ct) is { } product ? Results.Ok(ProductDto.From(product)) : Results.NotFound();

    private static async Task<IResult> Update(Guid id, ProductRequest r, HouseholdContext household, ProductLibrary products, CancellationToken ct) =>
        await products.UpdateAsync(household.HouseholdId, id, r, ct) is { } product ? Results.Ok(ProductDto.From(product)) : Results.NotFound();

    private static async Task<IResult> Archive(Guid id, HouseholdContext household, ProductLibrary products, CancellationToken ct) =>
        await products.ArchiveAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent();

    private static async Task<IResult> RecordPrice(Guid id, PriceRequest r, HouseholdContext household,
        RecordProductPrice useCase, CancellationToken ct) =>
        await useCase.ExecuteAsync(household.HouseholdId, id, r.StoreId, r.Price, r.ObservedOn, ct) is { } product
            ? Results.Ok(ProductDto.From(product)) : Results.NotFound();

    private static async Task<IResult> RemovePrice(Guid id, Guid priceId, HouseholdContext household,
        RemoveProductPrice useCase, CancellationToken ct) =>
        await useCase.ExecuteAsync(household.HouseholdId, id, priceId, ct) is { } product
            ? Results.Ok(ProductDto.From(product)) : Results.NotFound();
}

/// <summary>
/// Product management shared by the endpoints and the assistant tools (ADR 0011): plain CRUD on
/// the Food tables, rules in <see cref="Product"/>, scoped to one household.
/// </summary>
public sealed class ProductLibrary(LifeOSDbContext db)
{
    /// <param name="foodId">Optional: only the products of this food.</param>
    /// <param name="search">Optional: name or brand contains this text.</param>
    public async Task<List<Product>> ListAsync(Guid householdId, Guid? foodId = null, string? search = null,
        bool includeArchived = true, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Include(p => p.Prices).Where(p => p.HouseholdId == householdId);
        if (foodId is { } id) query = query.Where(p => p.FoodItemId == id);
        if (!includeArchived) query = query.Where(p => !p.IsArchived);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{Foods.FoodLibrary.EscapeLike(search.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, "\\") || EF.Functions.ILike(p.Brand ?? "", pattern, "\\"));
        }
        return await query.OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
    }

    public Task<Product?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.Products.Include(p => p.Prices).SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == householdId, ct);

    public async Task<Product> CreateAsync(Guid householdId, ProductRequest r, CancellationToken ct)
    {
        await RequireActiveFood(householdId, r.FoodItemId, ct);
        var product = Product.Create(householdId, r.FoodItemId, r.Name, r.PackageQuantity, r.PackageUnit, r.Brand, r.Barcode);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Product?> UpdateAsync(Guid householdId, Guid id, ProductRequest r, CancellationToken ct)
    {
        var product = await FindAsync(householdId, id, ct);
        if (product is null) return null;
        await RequireActiveFood(householdId, r.FoodItemId, ct);
        product.Update(r.FoodItemId, r.Name, r.PackageQuantity, r.PackageUnit, r.Brand, r.Barcode);
        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Product?> ArchiveAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var product = await FindAsync(householdId, id, ct);
        if (product is null) return null;
        product.Archive();
        await db.SaveChangesAsync(ct);
        return product;
    }

    private async Task RequireActiveFood(Guid householdId, Guid id, CancellationToken ct)
    {
        if (!await db.FoodItems.AnyAsync(f => f.Id == id && f.HouseholdId == householdId && !f.IsArchived, ct))
            throw new ArgumentException("Choisissez un aliment actif de votre foyer.");
    }
}
