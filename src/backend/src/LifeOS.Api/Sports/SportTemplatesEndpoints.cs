using LifeOS.Api.Households;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

public sealed record SportTemplateRequest(string Name, string Sport, int DurationMinutes,
    decimal? DistanceKm, string Intensity, decimal Calories);

public static class SportTemplatesEndpoints
{
    public static IEndpointRouteBuilder MapSportTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport-templates").RequireAuthorization().WithTags("Sport library");
        g.MapGet("/", List).Produces<List<SportTemplate>>();
        g.MapPost("/", Create).Produces<SportTemplate>(StatusCodes.Status201Created);
        g.MapPut("/{id:guid}", Update).Produces<SportTemplate>();
        g.MapDelete("/{id:guid}", Archive);
        return app;
    }

    private static Task<SportTemplate?> Find(LifeOSDbContext db, HouseholdContext household, Guid id, CancellationToken ct) =>
        db.SportTemplates.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == household.HouseholdId, ct);

    private static async Task<IResult> List(HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
        Results.Ok(await db.SportTemplates.Where(s => s.HouseholdId == household.HouseholdId).OrderBy(s => s.Name).ToListAsync(ct));

    private static async Task<IResult> Create(SportTemplateRequest r, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var template = SportTemplate.Create(household.HouseholdId, r.Name, r.Sport, r.DurationMinutes, r.DistanceKm, r.Intensity, r.Calories);
        db.SportTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/sport-templates/{template.Id}", template);
    }

    private static async Task<IResult> Update(Guid id, SportTemplateRequest r, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var template = await Find(db, household, id, ct);
        if (template is null) return Results.NotFound();
        template.Update(r.Name, r.Sport, r.DurationMinutes, r.DistanceKm, r.Intensity, r.Calories);
        await db.SaveChangesAsync(ct);
        return Results.Ok(template);
    }

    private static async Task<IResult> Archive(Guid id, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var template = await Find(db, household, id, ct);
        if (template is null) return Results.NotFound();
        template.Archive();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
