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
/// <param name="LoadKg">Load in kg by steps of 0.5; the exercise's usual load when omitted, 0 for bodyweight.</param>
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

/// <param name="Date">Day of the pass; today when omitted.</param>
public sealed record RoutineSessionStartRequest(DateOnly? Date);

/// <param name="SetsDone">Sets done, 0 (untick) to the sets of the target.</param>
public sealed record ExerciseSetsRequest([property: Range(0, 20)] int SetsDone);

/// <param name="SetsDone">Sets done; all the sets of the target when omitted.</param>
/// <param name="Value">Seconds or repetitions per set; the target when omitted.</param>
/// <param name="LoadKg">Load per set; the target's when omitted, 0 for bodyweight.</param>
public sealed record SessionExerciseRequest(Guid ExerciseId, [property: Range(1, 20)] int? SetsDone, int? Value, decimal? LoadKg);

/// <param name="Date">Day of the pass; today when omitted.</param>
/// <param name="Time">Start time (Europe/Paris); now, or noon for a past day, when omitted.</param>
public sealed record RoutineSessionLogRequest(DateOnly? Date, TimeOnly? Time,
    [property: Required] IReadOnlyList<SessionExerciseRequest> Exercises);

/// <summary>Ticks an exercise in the open pass of the day (opening one if needed), or in a given pass.</summary>
/// <param name="SetsDone">Sets done; all the sets of the target when omitted.</param>
/// <param name="Value">Seconds or repetitions per set really done; the target when omitted.</param>
/// <param name="LoadKg">Load really used; the target's when omitted, 0 for bodyweight.</param>
/// <param name="Date">Day done; today when omitted.</param>
/// <param name="SessionId">The pass to tick in; the open pass of the day (or a new one) when omitted.</param>
public sealed record ExerciseCompletionRequest(Guid ExerciseId, [property: Range(1, 20)] int? SetsDone, int? Value, decimal? LoadKg,
    DateOnly? Date, Guid? SessionId);

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
        g.MapDelete("/{id:guid}/completions/{exerciseId:guid}", async (Guid id, Guid exerciseId, Guid? sessionId, DateOnly? date,
                HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.UncompleteAsync(household.HouseholdId, id, exerciseId, sessionId, date, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/sessions", async (Guid id, RoutineSessionStartRequest request, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.StartSessionAsync(household.HouseholdId, id, request.Date, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/sessions/log", async (Guid id, RoutineSessionLogRequest request, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.LogSessionAsync(household.HouseholdId, id, request, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPut("/{id:guid}/sessions/{sessionId:guid}/exercises/{exerciseId:guid}", async (Guid id, Guid sessionId, Guid exerciseId,
                ExerciseSetsRequest request, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.SetSetsAsync(household.HouseholdId, id, sessionId, exerciseId, request.SetsDone, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/sessions/{sessionId:guid}", async (Guid id, Guid sessionId, HouseholdContext household, RoutineLibrary routines, CancellationToken ct) =>
            await routines.DeleteSessionAsync(household.HouseholdId, id, sessionId, ct) is { } report ? Results.Ok(report) : Results.NotFound())
            .Produces<RoutineReportDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>
/// Daily routines, their passes and ticks (ADR 0014, ADR 0015), shared by the endpoints and the assistant tools;
/// rules in <see cref="Routine"/> and <see cref="RoutineSession"/>.
/// </summary>
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

    /// <summary>Opens a pass on a day (today by default); refused while another pass of that day is unfinished.</summary>
    public async Task<RoutineReportDto?> StartSessionAsync(Guid householdId, Guid id, DateOnly? date, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        var day = date ?? SportClock.Today(clock);
        await StartAsync(routine, day, clock.GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    /// <summary>Sets how many sets of an exercise are done in a pass; 0 unticks it. A finished pass is not reopened.</summary>
    public async Task<RoutineReportDto?> SetSetsAsync(Guid householdId, Guid id, Guid sessionId, Guid exerciseId, int setsDone, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        var session = await FindSessionAsync(routine, sessionId, ct);
        if (session is null) return null;
        var ticks = await TicksAsync(session, ct);
        var finished = session.IsFinished(routine, ticks);
        var tick = ticks.SingleOrDefault(t => t.ExerciseId == exerciseId);
        var now = clock.GetUtcNow();
        if (setsDone == 0)
        {
            if (tick is not null)
            {
                if (finished) throw new ArgumentException("Ce passage est terminé ; supprimez-le pour le refaire.");
                db.ExerciseCompletions.Remove(tick);
            }
        }
        else if (tick is null) db.ExerciseCompletions.Add(ExerciseCompletion.Create(routine, session, exerciseId, setsDone, null, null, now, finished));
        else tick.SetSets(routine, setsDone, now, finished);
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    /// <summary>Deletes a pass with its ticks.</summary>
    public async Task<RoutineReportDto?> DeleteSessionAsync(Guid householdId, Guid id, Guid sessionId, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        var session = await FindSessionAsync(routine, sessionId, ct);
        if (session is null) return null;
        db.ExerciseCompletions.RemoveRange(await TicksAsync(session, ct));
        db.RoutineSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    /// <summary>Records a whole pass in one call (the assistant): each exercise with its sets and values.</summary>
    public async Task<RoutineReportDto?> LogSessionAsync(Guid householdId, Guid id, RoutineSessionLogRequest r, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        if (r.Exercises is not { Count: > 0 }) throw new ArgumentException("Indiquez au moins un exercice du passage.");
        if (r.Exercises.Select(e => e.ExerciseId).Distinct().Count() != r.Exercises.Count) throw new ArgumentException("Un exercice ne figure qu'une fois dans un passage.");
        var today = SportClock.Today(clock);
        var day = r.Date ?? today;
        var startedAt = r.Time is { } time ? SportClock.At(day, time) : day == today ? clock.GetUtcNow() : SportClock.At(day, new TimeOnly(12, 0));
        var session = await StartAsync(routine, day, startedAt, ct);
        foreach (var e in r.Exercises)
            db.ExerciseCompletions.Add(ExerciseCompletion.Create(routine, session, e.ExerciseId, e.SetsDone ?? routine.LineOn(day, e.ExerciseId).Sets,
                e.Value, e.LoadKg, startedAt, passFinished: false));
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    /// <summary>
    /// Ticks an exercise (the assistant): in the given pass, or the open pass of the day, or a new one. Sets,
    /// values given are recorded; the target is used otherwise.
    /// </summary>
    public async Task<RoutineReportDto?> CompleteAsync(Guid householdId, Guid id, ExerciseCompletionRequest r, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        var today = SportClock.Today(clock);
        var now = clock.GetUtcNow();
        RoutineSession session;
        if (r.SessionId is { } sessionId) session = await FindSessionAsync(routine, sessionId, ct) ?? throw new ArgumentException("Passage introuvable pour cette routine.");
        else
        {
            var day = r.Date ?? today;
            session = await OpenSessionAsync(routine, day, ct) ?? await StartAsync(routine, day, day == today ? now : SportClock.At(day, new TimeOnly(12, 0)), ct);
        }
        var ticks = db.Entry(session).State == EntityState.Added ? [] : await TicksAsync(session, ct);
        var finished = ticks.Count > 0 && session.IsFinished(routine, ticks);
        var sets = r.SetsDone ?? routine.LineOn(session.Date, r.ExerciseId).Sets;
        var tick = ticks.SingleOrDefault(t => t.ExerciseId == r.ExerciseId);
        if (tick is null)
        {
            tick = ExerciseCompletion.Create(routine, session, r.ExerciseId, sets, r.Value, r.LoadKg, now, finished);
            db.ExerciseCompletions.Add(tick);
        }
        else
        {
            if (tick.SetsDone != sets) tick.SetSets(routine, sets, now, finished);
            tick.Correct(routine, r.Value, r.LoadKg);
        }
        await db.SaveChangesAsync(ct);
        return await ReportAsync(routine, ct);
    }

    /// <summary>Unticks an exercise in a pass (by default the latest pass of the day where it is ticked); a finished pass is not reopened.</summary>
    public async Task<RoutineReportDto?> UncompleteAsync(Guid householdId, Guid id, Guid exerciseId, Guid? sessionId, DateOnly? date, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        RoutineSession? session;
        if (sessionId is { } sid) session = await FindSessionAsync(routine, sid, ct) ?? throw new ArgumentException("Passage introuvable pour cette routine.");
        else
        {
            var day = date ?? SportClock.Today(clock);
            session = await db.RoutineSessions
                .Where(s => s.RoutineId == id && s.HouseholdId == householdId && s.Date == day)
                .Where(s => db.ExerciseCompletions.Any(c => c.SessionId == s.Id && c.ExerciseId == exerciseId))
                .OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync(ct);
        }
        if (session is null) return await ReportAsync(routine, ct);
        return await SetSetsAsync(householdId, id, session.Id, exerciseId, 0, ct);
    }

    private async Task<RoutineSession> StartAsync(Routine routine, DateOnly day, DateTimeOffset startedAt, CancellationToken ct)
    {
        var session = RoutineSession.Start(routine, day, startedAt, SportClock.Today(clock), await OpenSessionAsync(routine, day, ct) is not null);
        db.RoutineSessions.Add(session);
        return session;
    }

    /// <summary>The unfinished pass of a day, if any.</summary>
    private async Task<RoutineSession?> OpenSessionAsync(Routine routine, DateOnly day, CancellationToken ct)
    {
        var sessions = await db.RoutineSessions.Where(s => s.RoutineId == routine.Id && s.HouseholdId == routine.HouseholdId && s.Date == day).ToListAsync(ct);
        foreach (var session in sessions.OrderByDescending(s => s.StartedAt))
            if (!session.IsFinished(routine, await TicksAsync(session, ct))) return session;
        return null;
    }

    private Task<RoutineSession?> FindSessionAsync(Routine routine, Guid sessionId, CancellationToken ct) =>
        db.RoutineSessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.RoutineId == routine.Id && s.HouseholdId == routine.HouseholdId, ct);

    private async Task<List<ExerciseCompletion>> TicksAsync(RoutineSession session, CancellationToken ct) =>
        await db.ExerciseCompletions.Where(c => c.SessionId == session.Id && c.HouseholdId == session.HouseholdId).ToListAsync(ct);

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
