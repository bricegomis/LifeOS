using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public static class HouseholdSocialEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var profiles = app.MapGroup("/api/households/profiles").RequireAuthorization().WithTags("Member profiles");
        profiles.MapPost("/", CreateProfileAsync);
        profiles.MapPut("/{id:guid}", UpdateProfileAsync);
        profiles.MapDelete("/{id:guid}", DeleteProfileAsync);
        return app;
    }

    private static async Task<Guid?> ResolveHousehold(ClaimsPrincipal user, ResolveHouseholdForUserQuery query, CancellationToken ct) =>
        user.TryGetUserId(out var userId) ? await query.ExecuteAsync(userId, ct) : null;

    private static async Task<IResult> CreateProfileAsync(CreateMemberProfileRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        try
        {
            var profile = MemberProfile.Create(household.Value, request.DisplayName, request.PortionCoefficient);
            db.MemberProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/households/profiles/{profile.Id}", ProfileDto(profile));
        }
        catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: 400); }
    }

    private static async Task<IResult> UpdateProfileAsync(Guid id, UpdateMemberProfileRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var profile = await db.MemberProfiles.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household, ct);
        if (profile is null) return Results.NotFound();
        try
        {
            profile.UpdateDetails(request.DisplayName, request.PortionCoefficient);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ProfileDto(profile));
        }
        catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: 400); }
    }

    private static async Task<IResult> DeleteProfileAsync(Guid id, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await ResolveHousehold(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var profile = await db.MemberProfiles.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household, ct);
        if (profile is null) return Results.NotFound();
        if (await db.FoodReviews.AnyAsync(r => r.MemberProfileId == id, ct)
            || await db.RecipeReviews.AnyAsync(r => r.MemberProfileId == id, ct))
            return Results.Problem("Ce profil est utilisé par des avis et ne peut pas être supprimé.", statusCode: 409);
        db.MemberProfiles.Remove(profile);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static MemberProfileDto ProfileDto(MemberProfile profile) =>
        new(profile.Id, profile.DisplayName, profile.PortionCoefficient, profile.CreatedAt, profile.UpdatedAt);
}

public sealed record CreateMemberProfileRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string DisplayName,
    [property: Range(typeof(decimal), "0.01", "100")] decimal PortionCoefficient = 1);
public sealed record UpdateMemberProfileRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string DisplayName,
    [property: Range(typeof(decimal), "0.01", "100")] decimal PortionCoefficient = 1);
