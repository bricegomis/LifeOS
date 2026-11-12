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
        g.MapGet("/foods/{foodId:guid}", ListFoodReviews);
        g.MapPost("/foods/{foodId:guid}", CreateFoodReview);
        g.MapPut("/foods/{foodId:guid}/{reviewId:guid}", UpdateFoodReview);
        g.MapDelete("/foods/{foodId:guid}/{reviewId:guid}", DeleteFoodReview);
        g.MapGet("/recipes/{recipeId:guid}", ListRecipeReviews);
        g.MapPost("/recipes/{recipeId:guid}", CreateRecipeReview);
        g.MapPut("/recipes/{recipeId:guid}/{reviewId:guid}", UpdateRecipeReview);
        g.MapDelete("/recipes/{recipeId:guid}/{reviewId:guid}", DeleteRecipeReview);
        return app;
    }

    private static async Task<Dictionary<Guid, string>> ProfileNames(LifeOSDbContext db, HouseholdContext household, CancellationToken ct) =>
        await db.MemberProfiles.Where(p => p.HouseholdId == household.HouseholdId).ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);

    private static async Task RequireProfile(LifeOSDbContext db, HouseholdContext household, Guid profileId, CancellationToken ct)
    {
        if (!await db.MemberProfiles.AnyAsync(p => p.Id == profileId && p.HouseholdId == household.HouseholdId, ct))
            throw new ArgumentException("Le profil doit appartenir à votre foyer.");
    }

    private static ReviewDto Dto(FoodReview r, IReadOnlyDictionary<Guid, string> names) => new(r.Id, r.FoodItemId, null,
        r.MemberProfileId, names.GetValueOrDefault(r.MemberProfileId), r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt);

    private static ReviewDto Dto(RecipeReview r, IReadOnlyDictionary<Guid, string> names) => new(r.Id, null, r.RecipeId,
        r.MemberProfileId, names.GetValueOrDefault(r.MemberProfileId), r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt);

    private static async Task<IResult> ListFoodReviews(Guid foodId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        if (!await db.FoodItems.AnyAsync(f => f.Id == foodId && f.HouseholdId == household.HouseholdId, ct)) return Results.NotFound();
        var reviews = await db.FoodReviews.AsNoTracking()
            .Where(r => r.FoodItemId == foodId && r.HouseholdId == household.HouseholdId).ToListAsync(ct);
        var names = await ProfileNames(db, household, ct);
        return Results.Ok(reviews.Select(r => Dto(r, names)).ToList());
    }

    private static async Task<IResult> CreateFoodReview(Guid foodId, SaveReviewRequest request, HouseholdContext household,
        LifeOSDbContext db, CancellationToken ct)
    {
        if (!await db.FoodItems.AnyAsync(f => f.Id == foodId && f.HouseholdId == household.HouseholdId && !f.IsArchived, ct))
            return Results.NotFound();
        await RequireProfile(db, household, request.MemberProfileId, ct);
        if (await db.FoodReviews.AnyAsync(r => r.FoodItemId == foodId && r.MemberProfileId == request.MemberProfileId, ct))
            return Results.Problem(DuplicateMessage, statusCode: StatusCodes.Status409Conflict);
        var review = FoodReview.Create(household.HouseholdId, foodId, request.MemberProfileId, request.Score, request.Comment, household.UserId);
        db.FoodReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/reviews/foods/{foodId}/{review.Id}", Dto(review, await ProfileNames(db, household, ct)));
    }

    private static async Task<IResult> UpdateFoodReview(Guid foodId, Guid reviewId, SaveReviewContentRequest request,
        HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var review = await db.FoodReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.FoodItemId == foodId && r.HouseholdId == household.HouseholdId, ct);
        if (review is null) return Results.NotFound();
        review.Update(request.Score, request.Comment);
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(review, await ProfileNames(db, household, ct)));
    }

    private static async Task<IResult> DeleteFoodReview(Guid foodId, Guid reviewId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var review = await db.FoodReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.FoodItemId == foodId && r.HouseholdId == household.HouseholdId, ct);
        if (review is null) return Results.NotFound();
        db.FoodReviews.Remove(review);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListRecipeReviews(Guid recipeId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        if (!await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == household.HouseholdId, ct)) return Results.NotFound();
        var reviews = await db.RecipeReviews.AsNoTracking()
            .Where(r => r.RecipeId == recipeId && r.HouseholdId == household.HouseholdId).ToListAsync(ct);
        var names = await ProfileNames(db, household, ct);
        return Results.Ok(reviews.Select(r => Dto(r, names)).ToList());
    }

    private static async Task<IResult> CreateRecipeReview(Guid recipeId, SaveReviewRequest request, HouseholdContext household,
        LifeOSDbContext db, CancellationToken ct)
    {
        if (!await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == household.HouseholdId && !r.IsArchived, ct))
            return Results.NotFound();
        await RequireProfile(db, household, request.MemberProfileId, ct);
        if (await db.RecipeReviews.AnyAsync(r => r.RecipeId == recipeId && r.MemberProfileId == request.MemberProfileId, ct))
            return Results.Problem(DuplicateMessage, statusCode: StatusCodes.Status409Conflict);
        var review = RecipeReview.Create(household.HouseholdId, recipeId, request.MemberProfileId, request.Score, request.Comment, household.UserId);
        db.RecipeReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/reviews/recipes/{recipeId}/{review.Id}", Dto(review, await ProfileNames(db, household, ct)));
    }

    private static async Task<IResult> UpdateRecipeReview(Guid recipeId, Guid reviewId, SaveReviewContentRequest request,
        HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var review = await db.RecipeReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.RecipeId == recipeId && r.HouseholdId == household.HouseholdId, ct);
        if (review is null) return Results.NotFound();
        review.Update(request.Score, request.Comment);
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(review, await ProfileNames(db, household, ct)));
    }

    private static async Task<IResult> DeleteRecipeReview(Guid recipeId, Guid reviewId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var review = await db.RecipeReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.RecipeId == recipeId && r.HouseholdId == household.HouseholdId, ct);
        if (review is null) return Results.NotFound();
        db.RecipeReviews.Remove(review);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
