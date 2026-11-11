using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Food.Stores;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Stores;

public sealed record StoreDto(Guid Id, string Name, string Address, bool IsOrganic, bool IsLocal,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static StoreDto From(Store s) => new(s.Id, s.Name, s.Address, s.IsOrganic, s.IsLocal, s.CreatedAt, s.UpdatedAt);
}

public sealed record StoreRequest(
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(400)] string Address,
    bool IsOrganic,
    bool IsLocal);

/// <summary>Simple management screen (ADR 0009): direct DbContext access, rules in <see cref="Store"/>.</summary>
public static class StoresEndpoints
{
    public static IEndpointRouteBuilder MapStoresEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stores").WithTags("Stores").RequireAuthorization().AddRequestValidation();
        g.MapGet("/", List).WithName("GetStores").Produces<List<StoreDto>>();
        g.MapGet("/{storeId:guid}", Get).WithName("GetStore").Produces<StoreDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/", Create).WithName("CreateStore").Produces<StoreDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{storeId:guid}", Update).WithName("UpdateStore").Produces<StoreDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{storeId:guid}", Delete).WithName("DeleteStore").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    private static Task<Store?> Find(LifeOSDbContext db, HouseholdContext household, Guid id, CancellationToken ct) =>
        db.Stores.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == household.HouseholdId, ct);

    private static async Task<IResult> List(HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var stores = await db.Stores.AsNoTracking().Where(s => s.HouseholdId == household.HouseholdId)
            .OrderBy(s => s.Name).ToListAsync(ct);
        return Results.Ok(stores.Select(StoreDto.From).ToList());
    }

    private static async Task<IResult> Get(Guid storeId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
        await Find(db, household, storeId, ct) is { } store ? Results.Ok(StoreDto.From(store)) : Results.NotFound();

    private static async Task<IResult> Create(StoreRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var store = Store.Create(household.HouseholdId, request.Name, request.Address ?? "", request.IsOrganic, request.IsLocal);
        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/stores/{store.Id}", StoreDto.From(store));
    }

    private static async Task<IResult> Update(Guid storeId, StoreRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var store = await Find(db, household, storeId, ct);
        if (store is null) return Results.NotFound();
        store.UpdateDetails(request.Name, request.Address ?? "", request.IsOrganic, request.IsLocal);
        await db.SaveChangesAsync(ct);
        return Results.Ok(StoreDto.From(store));
    }

    private static async Task<IResult> Delete(Guid storeId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var store = await Find(db, household, storeId, ct);
        if (store is null) return Results.NotFound();
        db.Stores.Remove(store);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
