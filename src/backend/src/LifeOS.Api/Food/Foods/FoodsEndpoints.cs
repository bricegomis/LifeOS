using LifeOS.Api.Food.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Food.Foods;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Foods;

/// <summary>Simple management screen (ADR 0009): direct DbContext access, rules in <see cref="FoodItem"/>.</summary>
public static class FoodsEndpoints
{
    public static IEndpointRouteBuilder MapFoodsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/foods").RequireAuthorization().WithTags("Aliments").AddRequestValidation();
        g.MapGet("/", List).Produces<List<FoodItemDto>>();
        g.MapPost("/", Create).Produces<FoodItemDto>(StatusCodes.Status201Created);
        g.MapGet("/{id:guid}", Get).Produces<FoodItemDto>();
        g.MapPut("/{id:guid}", Update).Produces<FoodItemDto>();
        g.MapDelete("/{id:guid}", Archive);
        g.MapGet("/{id:guid}/image", GetImage);
        g.MapPut("/{id:guid}/image", UploadImage).WithUploadLimit();
        g.MapDelete("/{id:guid}/image", DeleteImage);
        return app;
    }

    private static Task<FoodItem?> Find(LifeOSDbContext db, HouseholdContext household, Guid id, CancellationToken ct) =>
        db.FoodItems.SingleOrDefaultAsync(f => f.Id == id && f.HouseholdId == household.HouseholdId, ct);

    private static async Task<IResult> List(HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var foods = await db.FoodItems.AsNoTracking().Where(f => f.HouseholdId == household.HouseholdId)
            .OrderBy(f => f.Name).ToListAsync(ct);
        return Results.Ok(foods.Select(FoodItemDto.From).ToList());
    }

    private static async Task<IResult> Create(FoodRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var food = FoodItem.CreateManual(household.HouseholdId, request.Name, request.ReferenceUnit ?? "", request.Nutrition?.ToDomain());
        food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/foods/{food.Id}", FoodItemDto.From(food));
    }

    private static async Task<IResult> Get(Guid id, HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
        await Find(db, household, id, ct) is { } food ? Results.Ok(FoodItemDto.From(food)) : Results.NotFound();

    private static async Task<IResult> Update(Guid id, FoodRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var food = await Find(db, household, id, ct);
        if (food is null) return Results.NotFound();
        food.UpdateDetails(request.Name, request.ReferenceUnit ?? "", request.Nutrition?.ToDomain());
        food.UpdateDigestiveInformation(request.Fodmap, request.Nova, request.DigestiveNotes);
        await db.SaveChangesAsync(ct);
        return Results.Ok(FoodItemDto.From(food));
    }

    private static async Task<IResult> Archive(Guid id, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var food = await Find(db, household, id, ct);
        if (food is null) return Results.NotFound();
        food.Archive();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetImage(Guid id, HouseholdContext household, HttpContext context,
        LifeOSDbContext db, FoodImageStorage storage, CancellationToken ct)
    {
        var image = await db.FoodItems.Where(f => f.Id == id && f.HouseholdId == household.HouseholdId)
            .Select(f => new { f.ImagePath, f.ImageContentType }).SingleOrDefaultAsync(ct);
        return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
    }

    private static async Task<IResult> UploadImage(Guid id, HttpRequest request, HouseholdContext household,
        LifeOSDbContext db, FoodImageStorage storage, ILogger<FoodImageStorage> logger, CancellationToken ct)
    {
        var food = await Find(db, household, id, ct);
        if (food is null) return Results.NotFound();
        await storage.ReplaceImageAsync(request, food.ImagePath, stored => food.SetImage(stored.FileName, stored.ContentType),
            () => db.SaveChangesAsync(ct), logger, ct);
        return Results.Ok(FoodItemDto.From(food));
    }

    private static async Task<IResult> DeleteImage(Guid id, HouseholdContext household, LifeOSDbContext db,
        FoodImageStorage storage, ILogger<FoodImageStorage> logger, CancellationToken ct)
    {
        var food = await Find(db, household, id, ct);
        if (food is null) return Results.NotFound();
        var oldPath = food.ImagePath;
        food.RemoveImage();
        await db.SaveChangesAsync(ct);
        storage.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove deleted food image"));
        return Results.NoContent();
    }
}
