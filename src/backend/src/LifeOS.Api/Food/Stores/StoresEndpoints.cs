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

/// <summary>Simple management screen (ADR 0009): HTTP glue over <see cref="StoreLibrary"/>.</summary>
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

    private static async Task<IResult> List(HouseholdContext household, StoreLibrary stores, CancellationToken ct) =>
        Results.Ok((await stores.ListAsync(household.HouseholdId, ct)).Select(StoreDto.From).ToList());

    private static async Task<IResult> Get(Guid storeId, HouseholdContext household, StoreLibrary stores, CancellationToken ct) =>
        await stores.FindAsync(household.HouseholdId, storeId, ct) is { } store ? Results.Ok(StoreDto.From(store)) : Results.NotFound();

    private static async Task<IResult> Create(StoreRequest request, HouseholdContext household, StoreLibrary stores, CancellationToken ct)
    {
        var store = await stores.CreateAsync(household.HouseholdId, request, ct);
        return Results.Created($"/api/stores/{store.Id}", StoreDto.From(store));
    }

    private static async Task<IResult> Update(Guid storeId, StoreRequest request, HouseholdContext household, StoreLibrary stores, CancellationToken ct) =>
        await stores.UpdateAsync(household.HouseholdId, storeId, request, ct) is { } store ? Results.Ok(StoreDto.From(store)) : Results.NotFound();

    private static async Task<IResult> Delete(Guid storeId, HouseholdContext household, StoreLibrary stores, CancellationToken ct) =>
        await stores.DeleteAsync(household.HouseholdId, storeId, ct) ? Results.NoContent() : Results.NotFound();
}

/// <summary>
/// Store management shared by the endpoints and the assistant tools (ADR 0011): plain DbContext
/// access, rules in <see cref="Store"/>, scoped to one household.
/// </summary>
public sealed class StoreLibrary(LifeOSDbContext db)
{
    public async Task<List<Store>> ListAsync(Guid householdId, CancellationToken ct) =>
        await db.Stores.AsNoTracking().Where(s => s.HouseholdId == householdId).OrderBy(s => s.Name).ToListAsync(ct);

    public Task<Store?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.Stores.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == householdId, ct);

    public async Task<Store> CreateAsync(Guid householdId, StoreRequest request, CancellationToken ct)
    {
        var store = Store.Create(householdId, request.Name, request.Address ?? "", request.IsOrganic, request.IsLocal);
        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);
        return store;
    }

    public async Task<Store?> UpdateAsync(Guid householdId, Guid id, StoreRequest request, CancellationToken ct)
    {
        var store = await FindAsync(householdId, id, ct);
        if (store is null) return null;
        store.UpdateDetails(request.Name, request.Address ?? "", request.IsOrganic, request.IsLocal);
        await db.SaveChangesAsync(ct);
        return store;
    }

    /// <summary>A store still used by a price or a receipt is kept: the database refuses the delete (409).</summary>
    public async Task<bool> DeleteAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var store = await FindAsync(householdId, id, ct);
        if (store is null) return false;
        db.Stores.Remove(store);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
