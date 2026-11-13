using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

public sealed record SportTemplateRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Required] string Sport,
    [property: Range(1, 1440)] int DurationMinutes,
    [property: Range(0, 1000)] decimal? DistanceKm,
    [property: Range(0, 20000)] int Calories);

public sealed record SportTemplateDto(Guid Id, string Name, string Sport, string SportLabel, int DurationMinutes,
    decimal? DistanceKm, int Calories, bool IsArchived)
{
    public static SportTemplateDto From(SportTemplate t) =>
        new(t.Id, t.Name, t.Sport, SportCatalog.Sports[t.Sport], t.DurationMinutes, t.DistanceKm, t.Calories, t.IsArchived);
}

public static class SportTemplatesEndpoints
{
    public static IEndpointRouteBuilder MapSportTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport-templates").RequireAuthorization().WithTags("Sport").AddRequestValidation();
        g.MapGet("/", async (HouseholdContext household, SportTemplateLibrary templates, CancellationToken ct) =>
            Results.Ok(await templates.ListAsync(household.HouseholdId, includeArchived: true, ct))).Produces<List<SportTemplateDto>>();
        g.MapPost("/", async (SportTemplateRequest request, HouseholdContext household, SportTemplateLibrary templates, CancellationToken ct) =>
        {
            var template = await templates.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/sport-templates/{template.Id}", template);
        }).Produces<SportTemplateDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, SportTemplateRequest request, HouseholdContext household, SportTemplateLibrary templates, CancellationToken ct) =>
            await templates.UpdateAsync(household.HouseholdId, id, request, ct) is { } template ? Results.Ok(template) : Results.NotFound())
            .Produces<SportTemplateDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, SportTemplateLibrary templates, CancellationToken ct) =>
            await templates.ArchiveAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>Template management shared by the endpoints and the assistant tools; rules in <see cref="SportTemplate"/>.</summary>
public sealed class SportTemplateLibrary(LifeOSDbContext db)
{
    public async Task<List<SportTemplateDto>> ListAsync(Guid householdId, bool includeArchived, CancellationToken ct) =>
        (await db.SportTemplates.AsNoTracking()
            .Where(t => t.HouseholdId == householdId && (includeArchived || !t.IsArchived))
            .OrderBy(t => t.Name).ToListAsync(ct)).Select(SportTemplateDto.From).ToList();

    public async Task<SportTemplateDto> CreateAsync(Guid householdId, SportTemplateRequest r, CancellationToken ct)
    {
        var template = SportTemplate.Create(householdId, r.Name, r.Sport, r.DurationMinutes, r.DistanceKm, r.Calories);
        db.SportTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return SportTemplateDto.From(template);
    }

    public async Task<SportTemplateDto?> UpdateAsync(Guid householdId, Guid id, SportTemplateRequest r, CancellationToken ct)
    {
        var template = await db.SportTemplates.SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == householdId, ct);
        if (template is null) return null;
        template.Update(r.Name, r.Sport, r.DurationMinutes, r.DistanceKm, r.Calories);
        await db.SaveChangesAsync(ct);
        return SportTemplateDto.From(template);
    }

    public async Task<bool> ArchiveAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var template = await db.SportTemplates.SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == householdId, ct);
        if (template is null) return false;
        template.Archive();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
