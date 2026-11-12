using LifeOS.Api.Food.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Food.Foods;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Foods;

/// <summary>Simple management screen (ADR 0009): HTTP glue over <see cref="FoodLibrary"/>, shared with the assistant.</summary>
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

    private static async Task<IResult> List(HouseholdContext household, FoodLibrary foods, CancellationToken ct) =>
        Results.Ok((await foods.ListAsync(household.HouseholdId, ct: ct)).Select(FoodItemDto.From).ToList());

    private static async Task<IResult> Create(FoodRequest request, HouseholdContext household, FoodLibrary foods, CancellationToken ct)
    {
        var food = await foods.CreateAsync(household.HouseholdId, request, ct);
        return Results.Created($"/api/foods/{food.Id}", FoodItemDto.From(food));
    }

    private static async Task<IResult> Get(Guid id, HouseholdContext household, FoodLibrary foods, CancellationToken ct) =>
        await foods.FindAsync(household.HouseholdId, id, ct) is { } food ? Results.Ok(FoodItemDto.From(food)) : Results.NotFound();

    private static async Task<IResult> Update(Guid id, FoodRequest request, HouseholdContext household, FoodLibrary foods, CancellationToken ct) =>
        await foods.UpdateAsync(household.HouseholdId, id, request, ct) is { } food ? Results.Ok(FoodItemDto.From(food)) : Results.NotFound();

    private static async Task<IResult> Archive(Guid id, HouseholdContext household, FoodLibrary foods, CancellationToken ct) =>
        await foods.ArchiveAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent();

    private static async Task<IResult> GetImage(Guid id, HouseholdContext household, HttpContext context,
        LifeOSDbContext db, FoodImageStorage storage, CancellationToken ct)
    {
        var image = await db.FoodItems.Where(f => f.Id == id && f.HouseholdId == household.HouseholdId)
            .Select(f => new { f.ImagePath, f.ImageContentType }).SingleOrDefaultAsync(ct);
        return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
    }

    private static async Task<IResult> UploadImage(Guid id, HttpRequest request, HouseholdContext household,
        FoodLibrary foods, CancellationToken ct)
    {
        var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
        await using var content = file.OpenReadStream();
        return await foods.ReplaceImageAsync(household.HouseholdId, id, content, file.Length, file.ContentType, ct) is { } food
            ? Results.Ok(FoodItemDto.From(food)) : Results.NotFound();
    }

    private static async Task<IResult> DeleteImage(Guid id, HouseholdContext household, FoodLibrary foods, CancellationToken ct) =>
        await foods.RemoveImageAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent();
}
