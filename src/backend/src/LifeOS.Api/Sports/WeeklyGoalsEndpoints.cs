using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Sports;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

/// <param name="Kind">template, sport, sessions or volume.</param>
/// <param name="Unit">For volume goals: kcal, km or hours.</param>
/// <param name="MemberProfileId">The main profile when omitted. Ignored on update.</param>
public sealed record WeeklyGoalRequest(
    Guid? MemberProfileId,
    [property: Required] string Kind,
    Guid? TemplateId,
    string? Sport,
    string? Unit,
    [property: Range(0.01, 100000)] decimal Target);

public static class WeeklyGoalsEndpoints
{
    public static IEndpointRouteBuilder MapWeeklyGoalsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport/goals").RequireAuthorization().WithTags("Sport").AddRequestValidation();
        g.MapGet("/", async (Guid? profileId, HouseholdContext household, WeeklyGoalLibrary goals, CancellationToken ct) =>
            Results.Ok(await goals.ListActiveAsync(household.HouseholdId, profileId, ct))).Produces<List<WeeklyGoalDto>>();
        g.MapPost("/", async (WeeklyGoalRequest request, HouseholdContext household, WeeklyGoalLibrary goals, CancellationToken ct) =>
        {
            var goal = await goals.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/sport/goals/{goal.Id}", goal);
        }).Produces<WeeklyGoalDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, WeeklyGoalRequest request, HouseholdContext household, WeeklyGoalLibrary goals, CancellationToken ct) =>
            await goals.ChangeAsync(household.HouseholdId, id, request, ct) is { } goal ? Results.Ok(goal) : Results.NotFound())
            .Produces<WeeklyGoalDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, WeeklyGoalLibrary goals, CancellationToken ct) =>
            await goals.StopAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>Weekly goals, shared by the endpoints and the assistant tools; rules in <see cref="WeeklyGoal"/>.</summary>
public sealed class WeeklyGoalLibrary(LifeOSDbContext db, SportProfiles profiles, TimeProvider clock)
{
    public async Task<List<WeeklyGoalDto>> ListActiveAsync(Guid householdId, Guid? profileId, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, profileId, ct);
        var goals = await db.WeeklyGoals.AsNoTracking()
            .Where(g => g.HouseholdId == householdId && g.MemberProfileId == profile && g.ValidUntil == null)
            .OrderBy(g => g.ValidFrom).ToListAsync(ct);
        var names = await TemplateNamesAsync(householdId, ct);
        return goals.Select(g => WeeklyGoalDto.From(g, names)).ToList();
    }

    public async Task<WeeklyGoalDto> CreateAsync(Guid householdId, WeeklyGoalRequest r, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, r.MemberProfileId, ct);
        await CheckTemplateAsync(householdId, r, ct);
        var goal = WeeklyGoal.Create(householdId, profile, Values(r), SportClock.Today(clock));
        db.WeeklyGoals.Add(goal);
        await db.SaveChangesAsync(ct);
        return WeeklyGoalDto.From(goal, await TemplateNamesAsync(householdId, ct));
    }

    /// <summary>Changes a goal from the current week; past weeks keep the previous goal.</summary>
    public async Task<WeeklyGoalDto?> ChangeAsync(Guid householdId, Guid id, WeeklyGoalRequest r, CancellationToken ct)
    {
        var goal = await db.WeeklyGoals.SingleOrDefaultAsync(g => g.Id == id && g.HouseholdId == householdId, ct);
        if (goal is null) return null;
        await CheckTemplateAsync(householdId, r, ct);
        var replacement = goal.ChangeFrom(SportClock.Today(clock), Values(r));
        if (replacement is not null) db.WeeklyGoals.Add(replacement);
        await db.SaveChangesAsync(ct);
        return WeeklyGoalDto.From(replacement ?? goal, await TemplateNamesAsync(householdId, ct));
    }

    /// <summary>Stops a goal after the current week; a goal created this week is simply deleted.</summary>
    public async Task<bool> StopAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var goal = await db.WeeklyGoals.SingleOrDefaultAsync(g => g.Id == id && g.HouseholdId == householdId, ct);
        if (goal is null) return false;
        if (!goal.Stop(SportClock.Today(clock))) db.WeeklyGoals.Remove(goal);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static WeeklyGoalValues Values(WeeklyGoalRequest r) => new(r.Kind, r.TemplateId, r.Sport, r.Unit, r.Target);

    private async Task CheckTemplateAsync(Guid householdId, WeeklyGoalRequest r, CancellationToken ct)
    {
        if (r.Kind == "template" && r.TemplateId is { } id
            && !await db.SportTemplates.AnyAsync(t => t.Id == id && t.HouseholdId == householdId && !t.IsArchived, ct))
            throw new ArgumentException("Séance type introuvable dans votre foyer.");
    }

    private async Task<Dictionary<Guid, string>> TemplateNamesAsync(Guid householdId, CancellationToken ct) =>
        await db.SportTemplates.AsNoTracking().Where(t => t.HouseholdId == householdId).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
}
