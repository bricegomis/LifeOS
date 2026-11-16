using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Sports;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Files;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Sports;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

/// <param name="Measure">hold (seconds) or reps; frozen once the exercise has been done.</param>
public sealed record ExerciseRequest(
    [property: Required, StringLength(100)] string Name,
    [property: StringLength(Exercise.MaxInstructionsLength)] string? Instructions,
    [property: Required] string Measure,
    bool WithLoad);

public static class ExercisesEndpoints
{
    public static IEndpointRouteBuilder MapExercisesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport/exercises").RequireAuthorization().WithTags("Sport").AddRequestValidation();
        g.MapGet("/", async (bool? includeArchived, HouseholdContext household, ExerciseLibrary exercises, CancellationToken ct) =>
            Results.Ok(await exercises.ListAsync(household.HouseholdId, includeArchived ?? false, ct)))
            .Produces<List<ExerciseDto>>();
        g.MapPost("/", async (ExerciseRequest request, HouseholdContext household, ExerciseLibrary exercises, CancellationToken ct) =>
        {
            var exercise = await exercises.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/sport/exercises/{exercise.Id}", exercise);
        }).Produces<ExerciseDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, ExerciseRequest request, HouseholdContext household, ExerciseLibrary exercises, CancellationToken ct) =>
            await exercises.UpdateAsync(household.HouseholdId, id, request, ct) is { } exercise ? Results.Ok(exercise) : Results.NotFound())
            .Produces<ExerciseDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, ExerciseLibrary exercises, CancellationToken ct) =>
            await exercises.ArchiveAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        g.MapGet("/{id:guid}/image", async (Guid id, HouseholdContext household, HttpContext context, LifeOSDbContext db,
            SportImageStorage storage, CancellationToken ct) =>
        {
            var image = await db.Exercises.Where(e => e.Id == id && e.HouseholdId == household.HouseholdId)
                .Select(e => new { e.ImagePath, e.ImageContentType }).SingleOrDefaultAsync(ct);
            return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
        });
        g.MapPut("/{id:guid}/image", async (Guid id, HttpRequest request, HouseholdContext household, ExerciseLibrary exercises, CancellationToken ct) =>
        {
            var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
            await using var content = file.OpenReadStream();
            return await exercises.ReplaceImageAsync(household.HouseholdId, id, content, file.Length, file.ContentType, ct) is { } exercise
                ? Results.Ok(exercise) : Results.NotFound();
        }).WithUploadLimit().Produces<ExerciseDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/image", async (Guid id, HouseholdContext household, ExerciseLibrary exercises, CancellationToken ct) =>
            await exercises.RemoveImageAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        g.MapGet("/{id:guid}/progress", async (Guid id, Guid? profileId, int? weeks, HouseholdContext household,
            ExerciseLibrary exercises, CancellationToken ct) =>
            await exercises.ProgressAsync(household.HouseholdId, id, profileId, weeks ?? 12, ct) is { } progress
                ? Results.Ok(progress) : Results.NotFound())
            .Produces<ExerciseProgressDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>
/// The household's exercise library (ADR 0014), shared by the endpoints and the assistant tools;
/// rules in <see cref="Exercise"/>. A null or false result means "not in this household".
/// </summary>
public sealed class ExerciseLibrary(LifeOSDbContext db, SportImageStorage images, IRemoteImageSource remoteImages,
    SportProfiles profiles, SportReadContract contract, TimeProvider clock, ILogger<ExerciseLibrary> logger)
{
    public async Task<List<ExerciseDto>> ListAsync(Guid householdId, bool includeArchived, CancellationToken ct)
    {
        var exercises = await db.Exercises.AsNoTracking()
            .Where(e => e.HouseholdId == householdId && (includeArchived || !e.IsArchived))
            .OrderBy(e => e.Name).ToListAsync(ct);
        var done = await db.ExerciseCompletions.Where(c => c.HouseholdId == householdId).Select(c => c.ExerciseId).Distinct().ToListAsync(ct);
        return exercises.Select(e => ExerciseDto.From(e, done.Contains(e.Id))).ToList();
    }

    public async Task<ExerciseDto> CreateAsync(Guid householdId, ExerciseRequest r, CancellationToken ct)
    {
        var exercise = Exercise.Create(householdId, r.Name, r.Instructions, r.Measure, r.WithLoad);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync(ct);
        return ExerciseDto.From(exercise, measureLocked: false);
    }

    public async Task<ExerciseDto?> UpdateAsync(Guid householdId, Guid id, ExerciseRequest r, CancellationToken ct)
    {
        var exercise = await FindAsync(householdId, id, ct);
        if (exercise is null) return null;
        var done = await IsDoneAsync(exercise, ct);
        exercise.Update(r.Name, r.Instructions, r.Measure, r.WithLoad, done);
        await db.SaveChangesAsync(ct);
        return ExerciseDto.From(exercise, done);
    }

    /// <summary>Archives an exercise no running routine uses today; its history stays.</summary>
    public async Task<bool> ArchiveAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var exercise = await FindAsync(householdId, id, ct);
        if (exercise is null) return false;
        var today = SportClock.Today(clock);
        var running = await db.Routines.Where(r => r.HouseholdId == householdId && r.StoppedOn == null).ToListAsync(ct);
        if (running.FirstOrDefault(r => r.VersionOn(today).Lines.Any(line => line.ExerciseId == id)) is { } routine)
            throw new ArgumentException($"Cet exercice fait partie de la routine « {routine.Name} » ; retirez-le d'abord.");
        exercise.Archive();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ExerciseDto?> ReplaceImageAsync(Guid householdId, Guid id, Stream content, long length, string? contentType,
        CancellationToken ct)
    {
        var exercise = await FindAsync(householdId, id, ct);
        if (exercise is null) return null;
        await images.ReplaceImageAsync(content, length, contentType, exercise.ImagePath,
            stored => exercise.SetImage(stored.FileName, stored.ContentType), () => db.SaveChangesAsync(ct), logger, ct);
        return ExerciseDto.From(exercise, await IsDoneAsync(exercise, ct));
    }

    /// <summary>Downloads an image found by the assistant, then stores it like an upload.</summary>
    public async Task<ExerciseDto?> ReplaceImageFromUrlAsync(Guid householdId, Guid id, string url, CancellationToken ct)
    {
        if (await FindAsync(householdId, id, ct) is null) return null;
        var image = await remoteImages.DownloadAsync(url, ct);
        using var content = new MemoryStream(image.Bytes);
        return await ReplaceImageAsync(householdId, id, content, image.Bytes.Length, image.ContentType, ct);
    }

    public async Task<ExerciseDto?> RemoveImageAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var exercise = await FindAsync(householdId, id, ct);
        if (exercise is null) return null;
        var oldPath = exercise.ImagePath;
        exercise.RemoveImage();
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove deleted exercise image"));
        return ExerciseDto.From(exercise, await IsDoneAsync(exercise, ct));
    }

    public async Task<ExerciseProgressDto?> ProgressAsync(Guid householdId, Guid id, Guid? profileId, int weeks, CancellationToken ct)
    {
        if (!await db.Exercises.AnyAsync(e => e.Id == id && e.HouseholdId == householdId, ct)) return null;
        var profile = await profiles.ResolveAsync(householdId, profileId, ct);
        return await contract.ExerciseProgressAsync(householdId, profile, id, weeks, ct);
    }

    private Task<Exercise?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.Exercises.SingleOrDefaultAsync(e => e.Id == id && e.HouseholdId == householdId, ct);

    private Task<bool> IsDoneAsync(Exercise exercise, CancellationToken ct) =>
        db.ExerciseCompletions.AnyAsync(c => c.ExerciseId == exercise.Id && c.HouseholdId == exercise.HouseholdId, ct);
}
