using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reviews").RequireAuthorization().WithTags("Household reviews");
        group.MapGet("/foods/{foodId:guid}", GetFoodReviewsAsync);
        group.MapPost("/foods/{foodId:guid}", CreateFoodReviewAsync);
        group.MapPut("/foods/{foodId:guid}/{reviewId:guid}", UpdateFoodReviewAsync);
        group.MapDelete("/foods/{foodId:guid}/{reviewId:guid}", DeleteFoodReviewAsync);
        group.MapGet("/recipes/{recipeId:guid}", GetRecipeReviewsAsync);
        group.MapPost("/recipes/{recipeId:guid}", CreateRecipeReviewAsync);
        group.MapPut("/recipes/{recipeId:guid}/{reviewId:guid}", UpdateRecipeReviewAsync);
        group.MapDelete("/recipes/{recipeId:guid}/{reviewId:guid}", DeleteRecipeReviewAsync);
        group.MapGet("/planned-lines/{lineId:guid}", GetPlannedLineReviewsAsync);
        group.MapPost("/planned-lines/{lineId:guid}", CreatePlannedLineReviewAsync);
        group.MapPut("/planned-lines/{lineId:guid}/{reviewId:guid}", UpdatePlannedLineReviewAsync);
        group.MapDelete("/planned-lines/{lineId:guid}/{reviewId:guid}", DeletePlannedLineReviewAsync);
        return app;
    }

    private static async Task<Guid?> ResolveHousehold(ClaimsPrincipal user, ResolveHouseholdForUserQuery query, CancellationToken ct) =>
        user.TryGetUserId(out var userId) ? await query.ExecuteAsync(userId, ct) : null;

    private static IResult Invalid(Exception ex) => Results.Problem(ex.Message, statusCode: 400);
    private static IResult Duplicate() => Results.Problem("Un avis existe déjà pour cette cible. Modifiez l'avis existant.", statusCode: 409);

    private static async Task<bool> ValidTarget(LifeOSDbContext db, Guid household, Guid? profileId, Guid? groupId, CancellationToken ct)
    {
        if (profileId.HasValue == groupId.HasValue) return false;
        if (profileId is { } profile) return await db.MemberProfiles.AnyAsync(p => p.Id == profile && p.HouseholdId == household, ct);
        return await db.HouseholdGroups.AnyAsync(g => g.Id == groupId && g.HouseholdId == household, ct);
    }

    private static async Task<IResult> GetFoodReviewsAsync(Guid foodId, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        if (!await db.FoodItems.AnyAsync(f => f.Id == foodId && f.HouseholdId == household, ct)) return Results.NotFound();
        var items = await db.FoodReviews.Where(r => r.FoodItemId == foodId && r.HouseholdId == household)
            .Select(r => new { r.Id, r.FoodItemId, r.MemberProfileId, r.HouseholdGroupId, r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt })
            .ToListAsync(ct);
        var profileNames = await db.MemberProfiles.Where(p => p.HouseholdId == household)
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        var groupNames = await db.HouseholdGroups.Where(g => g.HouseholdId == household)
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        return Results.Ok(items.Select(r => new ReviewDto(r.Id, r.FoodItemId, null, null, r.MemberProfileId,
            r.HouseholdGroupId, r.MemberProfileId is { } p ? profileNames.GetValueOrDefault(p) : groupNames.GetValueOrDefault(r.HouseholdGroupId!.Value),
            r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt)));
    }

    private static async Task<IResult> CreateFoodReviewAsync(Guid foodId, SaveReviewRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var author)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(author, ct);
        if (!await db.FoodItems.AnyAsync(f => f.Id == foodId && f.HouseholdId == household && !f.IsArchived, ct)) return Results.NotFound();
        if (!await ValidTarget(db, household, request.MemberProfileId, request.HouseholdGroupId, ct))
            return Results.Problem("La personne ou le groupe doit appartenir à votre foyer.", statusCode: 400);
        try
        {
            var review = FoodReview.Create(household, foodId, request.MemberProfileId, request.HouseholdGroupId,
                request.Score, request.Comment, author);
            db.FoodReviews.Add(review);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/reviews/foods/{foodId}/{review.Id}", ReviewDto.From(review, null));
        }
        catch (ArgumentException ex) { return Invalid(ex); }
        catch (DbUpdateException) { return Duplicate(); }
    }

    private static async Task<IResult> UpdateFoodReviewAsync(Guid foodId, Guid reviewId, SaveReviewContentRequest request,
        ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var review = await db.FoodReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.FoodItemId == foodId && r.HouseholdId == household, ct);
        if (review is null) return Results.NotFound();
        try { review.Update(request.Score, request.Comment); await db.SaveChangesAsync(ct); return Results.Ok(ReviewDto.From(review, null)); }
        catch (ArgumentException ex) { return Invalid(ex); }
    }

    private static Task<IResult> DeleteFoodReviewAsync(Guid foodId, Guid reviewId, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        DeleteReviewAsync(user, households, db, ct, async household =>
        {
            var review = await db.FoodReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.FoodItemId == foodId && r.HouseholdId == household, ct);
            if (review is null) return Results.NotFound();
            db.FoodReviews.Remove(review);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

    private static async Task<IResult> GetRecipeReviewsAsync(Guid recipeId, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        if (!await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == household, ct)) return Results.NotFound();
        return Results.Ok(await RecipeReviewDtos(db, household.Value, recipeId, null, ct));
    }

    private static async Task<IResult> CreateRecipeReviewAsync(Guid recipeId, SaveReviewRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var author)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(author, ct);
        if (!await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == household && !r.IsArchived, ct)) return Results.NotFound();
        if (!await ValidTarget(db, household, request.MemberProfileId, request.HouseholdGroupId, ct))
            return Results.Problem("La personne ou le groupe doit appartenir à votre foyer.", statusCode: 400);
        try
        {
            var review = RecipeReview.Create(household, recipeId, null, request.MemberProfileId, request.HouseholdGroupId,
                request.Score, request.Comment, author);
            db.RecipeReviews.Add(review);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/reviews/recipes/{recipeId}/{review.Id}", ReviewDto.From(review, null));
        }
        catch (ArgumentException ex) { return Invalid(ex); }
        catch (DbUpdateException) { return Duplicate(); }
    }

    private static async Task<IResult> UpdateRecipeReviewAsync(Guid recipeId, Guid reviewId, SaveReviewContentRequest request,
        ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var review = await db.RecipeReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.RecipeId == recipeId && r.HouseholdId == household, ct);
        if (review is null) return Results.NotFound();
        try { review.Update(request.Score, request.Comment); await db.SaveChangesAsync(ct); return Results.Ok(ReviewDto.From(review, null)); }
        catch (ArgumentException ex) { return Invalid(ex); }
    }

    private static Task<IResult> DeleteRecipeReviewAsync(Guid recipeId, Guid reviewId, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        DeleteReviewAsync(user, households, db, ct, async household =>
        {
            var review = await db.RecipeReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.RecipeId == recipeId && r.HouseholdId == household, ct);
            if (review is null) return Results.NotFound();
            db.RecipeReviews.Remove(review);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

    private static async Task<IResult> GetPlannedLineReviewsAsync(Guid lineId, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        if (!await db.PlannedRecipeLines.AnyAsync(l => l.Id == lineId && l.HouseholdId == household, ct)) return Results.NotFound();
        return Results.Ok(await RecipeReviewDtos(db, household.Value, null, lineId, ct));
    }

    private static async Task<IResult> CreatePlannedLineReviewAsync(Guid lineId, SaveReviewRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        if (!user.TryGetUserId(out var author)) return Results.Unauthorized();
        var household = await households.ExecuteAsync(author, ct);
        if (!await db.PlannedRecipeLines.AnyAsync(l => l.Id == lineId && l.HouseholdId == household && l.RecipeId == null, ct)) return Results.NotFound();
        if (!await ValidTarget(db, household, request.MemberProfileId, request.HouseholdGroupId, ct))
            return Results.Problem("La personne ou le groupe doit appartenir à votre foyer.", statusCode: 400);
        try
        {
            var review = RecipeReview.Create(household, null, lineId, request.MemberProfileId, request.HouseholdGroupId,
                request.Score, request.Comment, author);
            db.RecipeReviews.Add(review);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/reviews/planned-lines/{lineId}/{review.Id}", ReviewDto.From(review, null));
        }
        catch (ArgumentException ex) { return Invalid(ex); }
        catch (DbUpdateException) { return Duplicate(); }
    }

    private static async Task<IResult> UpdatePlannedLineReviewAsync(Guid lineId, Guid reviewId, SaveReviewContentRequest request,
        ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var review = await db.RecipeReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.PlannedRecipeLineId == lineId && r.HouseholdId == household, ct);
        if (review is null) return Results.NotFound();
        try { review.Update(request.Score, request.Comment); await db.SaveChangesAsync(ct); return Results.Ok(ReviewDto.From(review, null)); }
        catch (ArgumentException ex) { return Invalid(ex); }
    }

    private static Task<IResult> DeletePlannedLineReviewAsync(Guid lineId, Guid reviewId, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        DeleteReviewAsync(user, households, db, ct, async household =>
        {
            var review = await db.RecipeReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.PlannedRecipeLineId == lineId && r.HouseholdId == household, ct);
            if (review is null) return Results.NotFound();
            db.RecipeReviews.Remove(review);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

    private static async Task<List<ReviewDto>> RecipeReviewDtos(LifeOSDbContext db, Guid household,
        Guid? recipeId, Guid? lineId, CancellationToken ct)
    {
        var items = await db.RecipeReviews.Where(r => r.HouseholdId == household
            && (recipeId.HasValue ? r.RecipeId == recipeId : r.PlannedRecipeLineId == lineId))
            .Select(r => new { r.Id, r.RecipeId, r.PlannedRecipeLineId, r.MemberProfileId, r.HouseholdGroupId,
                r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt }).ToListAsync(ct);
        var profiles = await db.MemberProfiles.Where(p => p.HouseholdId == household)
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        var groups = await db.HouseholdGroups.Where(g => g.HouseholdId == household)
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        return items.Select(r => new ReviewDto(r.Id, null, r.RecipeId, r.PlannedRecipeLineId, r.MemberProfileId,
            r.HouseholdGroupId, r.MemberProfileId is { } p ? profiles.GetValueOrDefault(p) : groups.GetValueOrDefault(r.HouseholdGroupId!.Value),
            r.Score, r.Comment, r.CreatedByUserId, r.UpdatedAt)).ToList();
    }

    private static async Task<IResult> DeleteReviewAsync(ClaimsPrincipal user, ResolveHouseholdForUserQuery households,
        LifeOSDbContext db, CancellationToken ct, Func<Guid, Task<IResult>> delete)
    {
        var household = await ResolveHousehold(user, households, ct);
        return household is null ? Results.Unauthorized() : await delete(household.Value);
    }
}

public sealed record SaveReviewRequest(
    Guid? MemberProfileId,
    Guid? HouseholdGroupId,
    [property: Range(0, 10)] int? Score,
    [property: StringLength(2000)] string? Comment);
public sealed record SaveReviewContentRequest(
    [property: Range(0, 10)] int? Score,
    [property: StringLength(2000)] string? Comment);
public sealed record ReviewDto(Guid Id, Guid? FoodItemId, Guid? RecipeId, Guid? PlannedRecipeLineId,
    Guid? MemberProfileId, Guid? HouseholdGroupId, string? TargetName, int? Score, string? Comment,
    Guid CreatedByUserId, DateTimeOffset UpdatedAt)
{
    public static ReviewDto From(FoodReview review, string? targetName) =>
        new(review.Id, review.FoodItemId, null, null, review.MemberProfileId, review.HouseholdGroupId,
            targetName, review.Score, review.Comment, review.CreatedByUserId, review.UpdatedAt);

    public static ReviewDto From(RecipeReview review, string? targetName) =>
        new(review.Id, null, review.RecipeId, review.PlannedRecipeLineId, review.MemberProfileId,
            review.HouseholdGroupId, targetName, review.Score, review.Comment, review.CreatedByUserId, review.UpdatedAt);
}
