using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Sports;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

/// <param name="MemberProfileId">Profile who did the session; the main profile when omitted. Ignored on update.</param>
public sealed record SportSessionRequest(
    Guid? MemberProfileId,
    DateOnly? Date,
    [property: Required] string Sport,
    Guid? TemplateId,
    [property: Range(1, 1440)] int DurationMinutes,
    [property: Range(0, 1000)] decimal? DistanceKm,
    [property: Range(0, 20000)] int? Calories,
    [property: Range(1, 10)] int? Feeling,
    [property: Range(30, 250)] int? AverageHeartRate,
    [property: Range(30, 250)] int? MaxHeartRate,
    [property: StringLength(4000)] string? Notes);

public static class SportSessionsEndpoints
{
    public static IEndpointRouteBuilder MapSportSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport/sessions").RequireAuthorization().WithTags("Sport").AddRequestValidation();
        g.MapGet("/", async (DateOnly from, DateOnly to, Guid? profileId, HouseholdContext household, SportSessionLibrary sessions, CancellationToken ct) =>
            Results.Ok(await sessions.ListAsync(household.HouseholdId, profileId, from, to, ct))).Produces<List<SportSessionDto>>();
        g.MapPost("/", async (SportSessionRequest request, HouseholdContext household, SportSessionLibrary sessions, CancellationToken ct) =>
        {
            var session = await sessions.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/sport/sessions/{session.Id}", session);
        }).Produces<SportSessionDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, SportSessionRequest request, HouseholdContext household, SportSessionLibrary sessions, CancellationToken ct) =>
            await sessions.UpdateAsync(household.HouseholdId, id, request, ct) is { } session ? Results.Ok(session) : Results.NotFound())
            .Produces<SportSessionDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, SportSessionLibrary sessions, CancellationToken ct) =>
            await sessions.DeleteAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>Logged sessions, shared by the endpoints and the assistant tools; rules in <see cref="SportSession"/>.</summary>
public sealed class SportSessionLibrary(LifeOSDbContext db, SportProfiles profiles, TimeProvider clock)
{
    public async Task<List<SportSessionDto>> ListAsync(Guid householdId, Guid? profileId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > 3660) throw new ArgumentException("Période invalide.");
        var profile = await profiles.ResolveAsync(householdId, profileId, ct);
        var sessions = await db.SportSessions.AsNoTracking()
            .Where(s => s.HouseholdId == householdId && s.MemberProfileId == profile && s.Date >= from && s.Date <= to)
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt).ToListAsync(ct);
        var names = await TemplateNamesAsync(householdId, ct);
        return sessions.Select(s => SportSessionDto.From(s, names)).ToList();
    }

    public async Task<SportSessionDto> CreateAsync(Guid householdId, SportSessionRequest r, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, r.MemberProfileId, ct);
        var today = SportClock.Today(clock);
        await CheckTemplateAsync(householdId, r.TemplateId, ct);
        var session = SportSession.Create(householdId, profile, Values(r, today), today, clock.GetUtcNow());
        db.SportSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return SportSessionDto.From(session, await TemplateNamesAsync(householdId, ct));
    }

    public async Task<SportSessionDto?> UpdateAsync(Guid householdId, Guid id, SportSessionRequest r, CancellationToken ct)
    {
        var session = await db.SportSessions.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == householdId, ct);
        if (session is null) return null;
        var today = SportClock.Today(clock);
        await CheckTemplateAsync(householdId, r.TemplateId, ct);
        session.Update(Values(r, today) with { Date = r.Date ?? session.Date }, today, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return SportSessionDto.From(session, await TemplateNamesAsync(householdId, ct));
    }

    public async Task<bool> DeleteAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var session = await db.SportSessions.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == householdId, ct);
        if (session is null) return false;
        db.SportSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static SportSessionValues Values(SportSessionRequest r, DateOnly today) => new(
        r.Date ?? today, r.Sport, r.TemplateId, r.DurationMinutes, r.DistanceKm, r.Calories, r.Feeling,
        r.AverageHeartRate, r.MaxHeartRate, r.Notes);

    private async Task CheckTemplateAsync(Guid householdId, Guid? templateId, CancellationToken ct)
    {
        if (templateId is { } id && !await db.SportTemplates.AnyAsync(t => t.Id == id && t.HouseholdId == householdId, ct))
            throw new ArgumentException("Séance type introuvable dans votre foyer.");
    }

    private async Task<Dictionary<Guid, string>> TemplateNamesAsync(Guid householdId, CancellationToken ct) =>
        await db.SportTemplates.AsNoTracking().Where(t => t.HouseholdId == householdId).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
}
