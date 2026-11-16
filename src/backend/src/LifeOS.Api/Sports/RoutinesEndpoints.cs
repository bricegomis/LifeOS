using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Sports;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

/// <param name="Target">Seconds or repetitions per set, following the exercise's measure.</param>
/// <param name="LoadKg">Only for an exercise with a load; 0 when omitted.</param>
public sealed record RoutineLineRequest(Guid ExerciseId, int Sets, int Target, decimal? LoadKg);

/// <param name="MemberProfileId">The main profile when omitted. Ignored on update.</param>
/// <param name="Days">Days of the week, 1 (Monday) to 7 (Sunday); every day when omitted or empty.</param>
/// <param name="Lines">Exercises in order, each once.</param>
public sealed record RoutineRequest(
    Guid? MemberProfileId,
    [property: Required, StringLength(100)] string Name,
    IReadOnlyList<int>? Days,
    [property: Range(0, 2)] int JokersPerWeek,
    [property: Required] IReadOnlyList<RoutineLineRequest> Lines);

/// <param name="Value">Seconds or repetitions really done; the target of the day when omitted.</param>
/// <param name="LoadKg">Load really used; the target's load when omitted.</param>
/// <param name="Date">Day done; today when omitted.</param>
public sealed record ExerciseCompletionRequest(Guid ExerciseId, int? Value, decimal? LoadKg, DateOnly? Date);

public static class RoutinesEndpoints
{
    public static IEndpointRouteBuilder MapRoutinesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport/routines").RequireAuthorization().WithTags("Sport").AddRequestValidation();
        g.MapGet("/", async (Guid? profileId, int? calendarWeeks, bool? includeStopped, HouseholdContext household,
                SportProfiles profiles, SportReadContract contract, CancellationToken ct) =>
            Results.Ok(await contract.RoutinesAsync(household.HouseholdId, await profiles.ResolveAsync(household.HouseholdId, profileId, ct),
                calendarWeeks ?? 1, includeStopped ?? false, ct)))
            .Produces<List<RoutineReportDto>>();
        g.MapPost("/", async (RoutineRequest request, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
        {
            var report = await routines.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/sport/routines/{report.Routine.Id}", report);
        }).Produces<RoutineReportDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, RoutineRequest request, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.UpdateAsync(household.HouseholdId, id, request, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.StopAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPut("/{id:guid}/completions", async (Guid id, ExerciseCompletionRequest request, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.CompleteAsync(household.HouseholdId, id, request, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/completions/{exerciseId:guid}", async (Guid id, Guid exerciseId, DateOnly? date, HouseholdContext household,
                RoutineLibrary routines, CancellationToken ct) =>
            await routines.UncompleteAsync(household.HouseholdId, id, exerciseId, date, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>Daily routines and their ticks (ADR 0014), shared by the endpoints and the assistant tools; rules in <see cref="Routine"/>.</summary>
public sealed class RoutineLibrary(LifeOSDbContext db, SportProfiles profiles, SportReadContract contract, TimeProvider clock)
{
    public async Task<RoutineReportDto> CreateAsync(Guid householdId, RoutineRequest r, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, r.MemberProfileId, ct);
        var routine = Routine.Create(householdId, profile, r.Name, DayBits(r.Days), r.JokersPerWeek,
            await LinesAsync(householdId, r.Lines, ct), SportClock.Today(clock));
        db.Routines.Add(routine);
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    /// <summary>Renames the routine; new days, jokers, exercises or targets apply from today.</summary>
    public async Task<RoutineReportDto?> UpdateAsync(Guid householdId, Guid id, RoutineRequest r, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        routine.Rename(r.Name);
        routine.Change(DayBits(r.Days), r.JokersPerWeek, await LinesAsync(householdId, r.Lines, ct), SportClock.Today(clock));
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    public async Task<bool> StopAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return false;
        routine.Stop(SportClock.Today(clock));
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Ticks an exercise, or corrects what was done that day, and returns the routine's report.</summary>
    public async Task<RoutineReportDto?> CompleteAsync(Guid householdId, Guid id, ExerciseCompletionRequest r, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        var exercise = await db.Exercises.SingleOrDefaultAsync(e => e.Id == r.ExerciseId && e.HouseholdId == householdId, ct)
            ?? throw new ArgumentException("Exercice introuvable dans votre foyer.");
        var today = SportClock.Today(clock);
        var day = r.Date ?? today;
        var existing = await db.ExerciseCompletions.SingleOrDefaultAsync(
            c => c.RoutineId == id && c.ExerciseId == exercise.Id && c.Date == day && c.HouseholdId == householdId, ct);
        if (existing is null) db.ExerciseCompletions.Add(ExerciseCompletion.Create(routine, exercise, day, r.Value, r.LoadKg, today, clock.GetUtcNow()));
        else existing.Change(routine, exercise, r.Value, r.LoadKg, today, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    /// <summary>Unticks an exercise; nothing happens when it was not ticked.</summary>
    public async Task<RoutineReportDto?> UncompleteAsync(Guid householdId, Guid id, Guid exerciseId, DateOnly? date, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        var day = date ?? SportClock.Today(clock);
        var existing = await db.ExerciseCompletions.SingleOrDefaultAsync(
            c => c.RoutineId == id && c.ExerciseId == exerciseId && c.Date == day && c.HouseholdId == householdId, ct);
        if (existing is not null)
        {
            db.ExerciseCompletions.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
        return await ReportAsync(routine, ct);
    }

    private static int DayBits(IReadOnlyList<int>? days)
    {
        if (days is null || days.Count == 0) return Routine.EveryDay;
        if (days.Any(day => day is < 1 or > 7)) throw new ArgumentException("Jours : 1 (lundi) à 7 (dimanche).");
        return days.Aggregate(0, (bits, day) => bits | (1 << (day - 1)));
    }

    /// <summary>Exercises are read in the caller's household; archived ones are refused.</summary>
    private async Task<List<RoutineLineInput>> LinesAsync(Guid householdId, IReadOnlyList<RoutineLineRequest>? lines, CancellationToken ct)
    {
        var ids = (lines ?? []).Select(line => line.ExerciseId).ToList();
        var exercises = await db.Exercises.Where(e => e.HouseholdId == householdId && ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        return (lines ?? []).Select(line =>
        {
            if (!exercises.TryGetValue(line.ExerciseId, out var exercise)) throw new ArgumentException("Exercice introuvable dans votre foyer.");
            if (exercise.IsArchived) throw new ArgumentException($"L'exercice « {exercise.Name} » est archivé.");
            return new RoutineLineInput(exercise, line.Sets, line.Target, line.LoadKg);
        }).ToList();
    }

    private Task<Routine?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.Routines.SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId, ct);

    private async Task<RoutineReportDto> ReportAsync(Routine routine, CancellationToken ct) =>
        (await contract.RoutinesAsync(routine.HouseholdId, routine.MemberProfileId, 1, includeStopped: true, ct))
        .Single(report => report.Routine.Id == routine.Id);
}
