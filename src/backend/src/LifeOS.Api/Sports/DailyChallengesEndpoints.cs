using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Sports;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

/// <param name="Unit">reps or minutes; fixed once created.</param>
/// <param name="MemberProfileId">The main profile when omitted. Ignored on update.</param>
public sealed record DailyChallengeRequest(
    Guid? MemberProfileId,
    [property: Required, StringLength(100)] string Name,
    [property: Required] string Unit,
    [property: Range(1, 100000)] int DailyTarget,
    [property: Range(0, 2)] int JokersPerWeek);

/// <param name="Date">Day of the set; today when omitted.</param>
public sealed record ChallengeEntryRequest([property: Range(1, 10000)] int Quantity, DateOnly? Date);

public static class DailyChallengesEndpoints
{
    public static IEndpointRouteBuilder MapDailyChallengesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport/challenges").RequireAuthorization().WithTags("Sport").AddRequestValidation();
        g.MapGet("/", async (Guid? profileId, int? calendarWeeks, bool? includeStopped, HouseholdContext household,
                SportProfiles profiles, SportReadContract contract, CancellationToken ct) =>
            Results.Ok(await contract.ChallengesAsync(household.HouseholdId, await profiles.ResolveAsync(household.HouseholdId, profileId, ct),
                calendarWeeks ?? 1, includeStopped ?? false, ct)))
            .Produces<List<ChallengeReportDto>>();
        g.MapPost("/", async (DailyChallengeRequest request, HouseholdContext household, DailyChallengeLibrary challenges, CancellationToken ct) =>
        {
            var challenge = await challenges.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/sport/challenges/{challenge.Id}", challenge);
        }).Produces<DailyChallengeDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, DailyChallengeRequest request, HouseholdContext household, DailyChallengeLibrary challenges, CancellationToken ct) =>
            await challenges.UpdateAsync(household.HouseholdId, id, request, ct) is { } challenge ? Results.Ok(challenge) : Results.NotFound())
            .Produces<DailyChallengeDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, DailyChallengeLibrary challenges, CancellationToken ct) =>
            await challenges.StopAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/entries", async (Guid id, ChallengeEntryRequest request, HouseholdContext household, DailyChallengeLibrary challenges, CancellationToken ct) =>
            await challenges.LogAsync(household.HouseholdId, id, request, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<ChallengeReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/entries/{entryId:guid}", async (Guid id, Guid entryId, HouseholdContext household, DailyChallengeLibrary challenges, CancellationToken ct) =>
            await challenges.RemoveEntryAsync(household.HouseholdId, id, entryId, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<ChallengeReportDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>Daily challenges and their sets, shared by the endpoints and the assistant tools; rules in <see cref="DailyChallenge"/>.</summary>
public sealed class DailyChallengeLibrary(LifeOSDbContext db, SportProfiles profiles, SportReadContract contract, TimeProvider clock)
{
    public async Task<DailyChallengeDto> CreateAsync(Guid householdId, DailyChallengeRequest r, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, r.MemberProfileId, ct);
        var today = SportClock.Today(clock);
        var challenge = DailyChallenge.Create(householdId, profile, r.Name, r.Unit, r.DailyTarget, r.JokersPerWeek, today);
        db.DailyChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
        return DailyChallengeDto.From(challenge, today);
    }

    /// <summary>Renames the challenge; a new target or joker count applies from today.</summary>
    public async Task<DailyChallengeDto?> UpdateAsync(Guid householdId, Guid id, DailyChallengeRequest r, CancellationToken ct)
    {
        var challenge = await FindAsync(householdId, id, ct);
        if (challenge is null) return null;
        if (r.Unit != challenge.Unit) throw new ArgumentException("L'unité d'un défi ne change pas ; créez un autre défi.");
        var today = SportClock.Today(clock);
        challenge.Rename(r.Name);
        challenge.ChangeRule(r.DailyTarget, r.JokersPerWeek, today);
        await db.SaveChangesAsync(ct);
        return DailyChallengeDto.From(challenge, today);
    }

    public async Task<bool> StopAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var challenge = await FindAsync(householdId, id, ct);
        if (challenge is null) return false;
        challenge.Stop(SportClock.Today(clock));
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Adds a set and returns the challenge's report, or null when the challenge is not in the household.</summary>
    public async Task<ChallengeReportDto?> LogAsync(Guid householdId, Guid id, ChallengeEntryRequest r, CancellationToken ct)
    {
        var challenge = await FindAsync(householdId, id, ct);
        if (challenge is null) return null;
        var today = SportClock.Today(clock);
        db.ChallengeEntries.Add(challenge.Log(r.Date ?? today, r.Quantity, today, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return await ReportAsync(challenge, ct);
    }

    /// <summary>Removes a mistyped set and returns the challenge's report.</summary>
    public async Task<ChallengeReportDto?> RemoveEntryAsync(Guid householdId, Guid id, Guid entryId, CancellationToken ct)
    {
        var challenge = await FindAsync(householdId, id, ct);
        var entry = await db.ChallengeEntries.SingleOrDefaultAsync(e => e.Id == entryId && e.ChallengeId == id && e.HouseholdId == householdId, ct);
        if (challenge is null || entry is null) return null;
        db.ChallengeEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return await ReportAsync(challenge, ct);
    }

    private Task<DailyChallenge?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.DailyChallenges.SingleOrDefaultAsync(c => c.Id == id && c.HouseholdId == householdId, ct);

    private async Task<ChallengeReportDto> ReportAsync(DailyChallenge challenge, CancellationToken ct) =>
        (await contract.ChallengesAsync(challenge.HouseholdId, challenge.MemberProfileId, 1, includeStopped: true, ct))
        .Single(report => report.Challenge.Id == challenge.Id);
}
