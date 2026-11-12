using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Households;

public sealed record CreateMemberProfileRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string DisplayName,
    [property: Range(typeof(decimal), "0.01", "100")] decimal PortionCoefficient = 1);
public sealed record UpdateMemberProfileRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string DisplayName,
    [property: Range(typeof(decimal), "0.01", "100")] decimal PortionCoefficient = 1);

/// <summary>
/// Household profiles (core). Deleting a profile still used by a module (reviews) is refused by
/// the database foreign key and surfaces as 409 through the global exception handler.
/// </summary>
public static class ProfilesEndpoints
{
    public static IEndpointRouteBuilder MapProfilesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/households/profiles").RequireAuthorization().WithTags("Member profiles").AddRequestValidation();
        g.MapPost("/", Create);
        g.MapPut("/{id:guid}", Update);
        g.MapDelete("/{id:guid}", Delete);
        return app;
    }

    private static MemberProfileDto Dto(MemberProfile p) => new(p.Id, p.DisplayName, p.PortionCoefficient, p.CreatedAt, p.UpdatedAt);

    private static Task<MemberProfile?> Find(LifeOSDbContext db, HouseholdContext household, Guid id, CancellationToken ct) =>
        db.MemberProfiles.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == household.HouseholdId, ct);

    private static async Task<IResult> Create(CreateMemberProfileRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var profile = MemberProfile.Create(household.HouseholdId, request.DisplayName, request.PortionCoefficient);
        db.MemberProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/households/profiles/{profile.Id}", Dto(profile));
    }

    private static async Task<IResult> Update(Guid id, UpdateMemberProfileRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var profile = await Find(db, household, id, ct);
        if (profile is null) return Results.NotFound();
        profile.UpdateDetails(request.DisplayName, request.PortionCoefficient);
        await db.SaveChangesAsync(ct);
        return Results.Ok(Dto(profile));
    }

    private static async Task<IResult> Delete(Guid id, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var profile = await Find(db, household, id, ct);
        if (profile is null) return Results.NotFound();
        db.MemberProfiles.Remove(profile);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
