using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Food.Reviews;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Reviews;

public sealed record SaveReviewRequest(
    Guid MemberProfileId,
    [property: Range(0, 10)] int? Score,
    [property: StringLength(2000)] string? Comment);
public sealed record SaveReviewContentRequest(
    [property: Range(0, 10)] int? Score,
    [property: StringLength(2000)] string? Comment);
public sealed record ReviewDto(Guid Id, Guid? FoodItemId, Guid? RecipeId,
    Guid MemberProfileId, string? ProfileName, int? Score, string? Comment,
    Guid CreatedByUserId, DateTimeOffset UpdatedAt);

/// <summary>What a review targets: exactly one food or one recipe of the household.</summary>
public readonly record struct ReviewTarget(Guid? FoodId, Guid? RecipeId)
{
    public static ReviewTarget Food(Guid id) => new(id, null);
    public static ReviewTarget Recipe(Guid id) => new(null, id);
}

/// <summary>
/// One review per household profile and per food or recipe (ADR 0010). Profiles belong to the
/// household core, which a domain module may reference.
/// </summary>
public static class ReviewsEndpoints
{
    private const string DuplicateMessage = "Un avis existe déjà pour cette cible. Modifiez l'avis existant.";

    public static IEndpointRouteBuilder MapReviewsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reviews").RequireAuthorization().WithTags("Household reviews").AddRequestValidation();
        g.MapGet("/foods/{foodId:guid}", (Guid foodId, HouseholdContext h, ReviewLibrary r, CancellationToken ct) => List(ReviewTarget.Food(foodId), h, r, ct));
        g.MapPost("/foods/{foodId:guid}", (Guid foodId, SaveReviewRequest request, HouseholdContext h, ReviewLibrary r, CancellationToken ct) =>
            Create(ReviewTarget.Food(foodId), request, h, r, ct));
        g.MapPut("/foods/{foodId:guid}/{reviewId:guid}", (Guid foodId, Guid reviewId, SaveReviewContentRequest request, HouseholdContext h, ReviewLibrary r, CancellationToken ct) =>
            Update(ReviewTarget.Food(foodId), reviewId, request, h, r, ct));
        g.MapDelete("/foods/{foodId:guid}/{reviewId:guid}", (Guid foodId, Guid reviewId, HouseholdContext h, ReviewLibrary r, CancellationToken ct) =>
            Delete(ReviewTarget.Food(foodId), reviewId, h, r, ct));
        g.MapGet("/recipes/{recipeId:guid}", (Guid recipeId, HouseholdContext h, ReviewLibrary r, CancellationToken ct) => List(ReviewTarget.Recipe(recipeId), h, r, ct));
        g.MapPost("/recipes/{recipeId:guid}", (Guid recipeId, SaveReviewRequest request, HouseholdContext h, ReviewLibrary r, CancellationToken ct) =>
            Create(ReviewTarget.Recipe(recipeId), request, h, r, ct));
        g.MapPut("/recipes/{recipeId:guid}/{reviewId:guid}", (Guid recipeId, Guid reviewId, SaveReviewContentRequest request, HouseholdContext h, ReviewLibrary r, CancellationToken ct) =>
            Update(ReviewTarget.Recipe(recipeId), reviewId, request, h, r, ct));
        g.MapDelete("/recipes/{recipeId:guid}/{reviewId:guid}", (Guid recipeId, Guid reviewId, HouseholdContext h, ReviewLibrary r, CancellationToken ct) =>
            Delete(ReviewTarget.Recipe(recipeId), reviewId, h, r, ct));
        return app;
    }

    private static async Task<IResult> List(ReviewTarget target, HouseholdContext household, ReviewLibrary reviews, CancellationToken ct) =>
        await reviews.ListAsync(household.HouseholdId, target, ct) is { } list ? Results.Ok(list) : Results.NotFound();

    private static async Task<IResult> Create(ReviewTarget target, SaveReviewRequest request, HouseholdContext household,
        ReviewLibrary reviews, CancellationToken ct)
    {
        var result = await reviews.CreateAsync(household, target, request, ct);
        if (result is null) return Results.NotFound();
        if (result.AlreadyExists) return Results.Problem(DuplicateMessage, statusCode: StatusCodes.Status409Conflict);
        var path = target.FoodId is { } foodId ? $"foods/{foodId}" : $"recipes/{target.RecipeId}";
        return Results.Created($"/api/reviews/{path}/{result.Review!.Id}", result.Review);
    }

    private static async Task<IResult> Update(ReviewTarget target, Guid reviewId, SaveReviewContentRequest request,
        HouseholdContext household, ReviewLibrary reviews, CancellationToken ct) =>
        await reviews.UpdateAsync(household.HouseholdId, target, reviewId, request, ct) is { } review ? Results.Ok(review) : Results.NotFound();

    private static async Task<IResult> Delete(ReviewTarget target, Guid reviewId, HouseholdContext household,
        ReviewLibrary reviews, CancellationToken ct) =>
        await reviews.DeleteAsync(household.HouseholdId, target, reviewId, ct) ? Results.NoContent() : Results.NotFound();
}

/// <summary>A created review, or <see cref="AlreadyExists"/> when the profile already reviewed the target.</summary>
public sealed record ReviewCreation(ReviewDto? Review, bool AlreadyExists);

/// <summary>
/// Review management shared by the endpoints and the assistant tools (ADR 0011), scoped to one
/// household. Null results mean the target or the review is not in the household.
/// </summary>
public sealed class ReviewLibrary(LifeOSDbContext db)
{
    public async Task<List<ReviewDto>?> ListAsync(Guid householdId, ReviewTarget target, CancellationToken ct)
    {
        if (!await TargetExists(householdId, target, activeOnly: false, ct)) return null;
        var names = await ProfileNames(householdId, ct);
        if (target.FoodId is { } foodId)
            return (await db.FoodReviews.AsNoTracking().Where(r => r.FoodItemId == foodId && r.HouseholdId == householdId).ToListAsync(ct))
                .Select(r => Dto(r, names)).ToList();
        return (await db.RecipeReviews.AsNoTracking().Where(r => r.RecipeId == target.RecipeId && r.HouseholdId == householdId).ToListAsync(ct))
            .Select(r => Dto(r, names)).ToList();
    }

    public async Task<ReviewCreation?> CreateAsync(HouseholdContext household, ReviewTarget target, SaveReviewRequest request, CancellationToken ct)
    {
        if (!await TargetExists(household.HouseholdId, target, activeOnly: true, ct)) return null;
        await RequireProfile(household.HouseholdId, request.MemberProfileId, ct);
        if (await FindByProfile(household.HouseholdId, target, request.MemberProfileId, ct) is not null) return new ReviewCreation(null, true);
        return new ReviewCreation(await Add(household, target, request, ct), false);
    }

    /// <summary>Creates the profile's review of the target, or replaces its score and comment.</summary>
    public async Task<ReviewDto?> SaveAsync(HouseholdContext household, ReviewTarget target, SaveReviewRequest request, CancellationToken ct)
    {
        if (!await TargetExists(household.HouseholdId, target, activeOnly: true, ct)) return null;
        await RequireProfile(household.HouseholdId, request.MemberProfileId, ct);
        switch (await FindByProfile(household.HouseholdId, target, request.MemberProfileId, ct))
        {
            case FoodReview food: food.Update(request.Score, request.Comment); break;
            case RecipeReview recipe: recipe.Update(request.Score, request.Comment); break;
            default: return await Add(household, target, request, ct);
        }
        await db.SaveChangesAsync(ct);
        return (await ListAsync(household.HouseholdId, target, ct))!.Single(r => r.MemberProfileId == request.MemberProfileId);
    }

    public async Task<ReviewDto?> UpdateAsync(Guid householdId, ReviewTarget target, Guid reviewId, SaveReviewContentRequest request,
        CancellationToken ct)
    {
        switch (await Find(householdId, target, reviewId, ct))
        {
            case FoodReview food: food.Update(request.Score, request.Comment); await db.SaveChangesAsync(ct); return Dto(food, await ProfileNames(householdId, ct));
            case RecipeReview recipe: recipe.Update(request.Score, request.Comment); await db.SaveChangesAsync(ct); return Dto(recipe, await ProfileNames(householdId, ct));
            default: return null;
        }
    }

    public async Task<bool> DeleteAsync(Guid householdId, ReviewTarget target, Guid reviewId, CancellationToken ct)
    {
        switch (await Find(householdId, target, reviewId, ct))
        {
            case FoodReview food: db.FoodReviews.Remove(food); break;
            case RecipeReview recipe: db.RecipeReviews.Remove(recipe); break;
            default: return false;
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<ReviewDto> Add(HouseholdContext household, ReviewTarget target, SaveReviewRequest request, CancellationToken ct)
    {
        var names = await ProfileNames(household.HouseholdId, ct);
        if (target.FoodId is { } foodId)
        {
            var review = FoodReview.Create(household.HouseholdId, foodId, request.MemberProfileId, request.Score, request.Comment, household.UserId);
            db.FoodReviews.Add(review);
            await db.SaveChangesAsync(ct);
            return Dto(review, names);
        }
        var recipeReview = RecipeReview.Create(household.HouseholdId, target.RecipeId!.Value, request.MemberProfileId, request.Score, request.Comment, household.UserId);
        db.RecipeReviews.Add(recipeReview);
        await db.SaveChangesAsync(ct);
        return Dto(recipeReview, names);
    }

    private async Task<object?> Find(Guid householdId, ReviewTarget target, Guid reviewId, CancellationToken ct) =>
        target.FoodId is { } foodId
            ? await db.FoodReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.FoodItemId == foodId && r.HouseholdId == householdId, ct)
            : await db.RecipeReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.RecipeId == target.RecipeId && r.HouseholdId == householdId, ct);

    private async Task<object?> FindByProfile(Guid householdId, ReviewTarget target, Guid profileId, CancellationToken ct) =>
        target.FoodId is { } foodId
            ? await db.FoodReviews.SingleOrDefaultAsync(r => r.FoodItemId == foodId && r.MemberProfileId == profileId && r.HouseholdId == householdId, ct)
            : await db.RecipeReviews.SingleOrDefaultAsync(r => r.RecipeId == target.RecipeId && r.MemberProfileId == profileId && r.HouseholdId == householdId, ct);

    private Task<bool> TargetExists(Guid householdId, ReviewTarget target, bool activeOnly, CancellationToken ct) =>
        target.FoodId is { } foodId
            ? db.FoodItems.AnyAsync(f => f.Id == foodId && f.HouseholdId == householdId && (!activeOnly || !f.IsArchived), ct)
            : db.Recipes.AnyAsync(r => r.Id == target.RecipeId && r.HouseholdId == householdId && (!activeOnly || !r.IsArchived), ct);

    private async Task RequireProfile(Guid householdId, Guid profileId, CancellationToken ct)
    {
        if (!await db.MemberProfiles.AnyAsync(p => p.Id == profileId && p.HouseholdId == householdId, ct))
            throw new ArgumentException("Le profil doit appartenir à votre foyer.");
    }

    private async Task<Dictionary<Guid, string>> ProfileNames(Guid householdId, CancellationToken ct) =>
        await db.MemberProfiles.Where(p => p.HouseholdId == householdId).ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);

    private static ReviewDto Dto(FoodReview r, IReadOnlyDictionary<Guid, string> names) => new(r.Id, r.FoodItemId, null,
        r.MemberProfileId, names.GetValueOrDefault(r.MemberProfileId), r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt);

    private static ReviewDto Dto(RecipeReview r, IReadOnlyDictionary<Guid, string> names) => new(r.Id, null, r.RecipeId,
        r.MemberProfileId, names.GetValueOrDefault(r.MemberProfileId), r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt);
}
