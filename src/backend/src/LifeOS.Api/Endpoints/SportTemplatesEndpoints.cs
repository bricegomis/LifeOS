using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;
using LifeOS.Domain.WeekPlanning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public static class SportTemplatesEndpoints
{
    public record SportTemplateRequest(string Name, string Sport, int DurationMinutes,
        decimal? DistanceKm, string Intensity, decimal Calories);

    public static IEndpointRouteBuilder MapSportTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sport-templates").RequireAuthorization().WithTags("Sport library");
        group.MapGet("/", async (ClaimsPrincipal user, ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            return Results.Ok(await db.SportTemplates.Where(s => s.HouseholdId == household).OrderBy(s => s.Name).ToListAsync(ct));
        }).Produces<List<SportTemplate>>();
        group.MapPost("/", async (SportTemplateRequest request, ClaimsPrincipal user,
            ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var template = SportTemplate.Create(household, request.Name, request.Sport, request.DurationMinutes,
                request.DistanceKm, request.Intensity, request.Calories);
            db.SportTemplates.Add(template);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/sport-templates/{template.Id}", template);
        }).Produces<SportTemplate>(201);
        group.MapPut("/{id:guid}", async (Guid id, SportTemplateRequest request, ClaimsPrincipal user,
            ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var template = await db.SportTemplates.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == household, ct);
            if (template is null) return Results.NotFound();
            template.Update(request.Name, request.Sport, request.DurationMinutes, request.DistanceKm, request.Intensity, request.Calories);
            await db.SaveChangesAsync(ct);
            return Results.Ok(template);
        }).Produces<SportTemplate>();
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user,
            ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct) =>
        {
            if (!user.TryGetUserId(out var sub)) return Results.Unauthorized();
            var household = await households.ExecuteAsync(sub, ct);
            var template = await db.SportTemplates.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == household, ct);
            if (template is null) return Results.NotFound();
            template.Archive();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        return app;
    }
}
