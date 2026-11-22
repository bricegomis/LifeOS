using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Tasks;
using LifeOS.Application.Tasks.Contracts;
using LifeOS.Domain.Tasks;
using LifeOS.Infrastructure.Files;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Tasks;

/// <param name="Kind">fixed (dates) or delay (days after the last pass).</param>
/// <param name="Unit">day, week or month, for fixed dates.</param>
/// <param name="Every">Every N days, weeks or months (1 to 365).</param>
/// <param name="Days">Days of the week, 1 (Monday) to 7 (Sunday), for a weekly recurrence.</param>
/// <param name="MonthDay">Day of the month (1 to 31; the 31st falls on the last day of a shorter month).</param>
/// <param name="DelayDays">Days after the last pass (1 to 365), for a delay recurrence.</param>
public sealed record RecurrenceRequest(string Kind, string? Unit, int? Every, IReadOnlyList<int>? Days, int? MonthDay, int? DelayDays)
{
    public Recurrence ToRecurrence() => Kind?.Trim() switch
    {
        "delay" => Recurrence.Delay(DelayDays ?? 0),
        "fixed" => Recurrence.Fixed(Unit?.Trim() ?? "", Every ?? 1, Days, MonthDay ?? 1),
        _ => throw new ArgumentException("Récurrence : fixed (dates fixes) ou delay (délai après la dernière fois)."),
    };
}

/// <param name="MemberProfileId">The main profile when omitted. Ignored on update.</param>
/// <param name="StartOn">The first possible due day; today when omitted. Fixed dates are counted from it.</param>
public sealed record TaskRoutineRequest(
    Guid? MemberProfileId,
    [property: Required, StringLength(Todo.MaxTitleLength)] string Title,
    [property: StringLength(Todo.MaxNotesLength)] string? Notes,
    [property: Range(1, Todo.MaxDurationMinutes)] int? DurationMinutes,
    string? Priority,
    IReadOnlyList<string>? Tags,
    [property: Required] RecurrenceRequest Recurrence,
    DateOnly? StartOn);

/// <param name="Day">Day it was done; today when omitted, never in the future nor before the last pass.</param>
public sealed record RoutineDoneRequest(DateOnly? Day, [property: StringLength(TaskRoutine.MaxNoteLength)] string? Note);

public static class TaskRoutinesEndpoints
{
    public static IEndpointRouteBuilder MapTaskRoutinesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tasks/routines").RequireAuthorization().WithTags("Tâches").AddRequestValidation();
        g.MapGet("/", async (string? state, string? q, string? tag, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            Results.Ok(await routines.SearchAsync(household.HouseholdId, state, q, tag, ct)))
            .Produces<List<TaskRoutineDto>>().ProducesValidationProblem();
        g.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            await routines.GetAsync(household.HouseholdId, id, ct) is { } routine ? Results.Ok(routine) : Results.NotFound())
            .Produces<TaskRoutineDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/", async (TaskRoutineRequest request, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
        {
            var routine = await routines.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/tasks/routines/{routine.Routine.Id}", routine);
        }).Produces<TaskRoutineDetailDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, TaskRoutineRequest request, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            Found(await routines.UpdateAsync(household.HouseholdId, id, request, ct)))
            .Produces<TaskRoutineDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/done", async (Guid id, RoutineDoneRequest? request, HouseholdContext household, TaskRoutineLibrary routines,
            CancellationToken ct) => Found(await routines.CompleteAsync(household.HouseholdId, id, request?.Day, request?.Note, ct)))
            .Produces<TaskRoutineDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/skip", async (Guid id, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            Found(await routines.SkipAsync(household.HouseholdId, id, ct)))
            .Produces<TaskRoutineDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/passes/{passId:guid}", async (Guid id, Guid passId, HouseholdContext household, TaskRoutineLibrary routines,
            CancellationToken ct) => Found(await routines.RemovePassAsync(household.HouseholdId, id, passId, ct)))
            .Produces<TaskRoutineDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/pause", async (Guid id, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            Found(await routines.PauseAsync(household.HouseholdId, id, ct)))
            .Produces<TaskRoutineDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/resume", async (Guid id, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            Found(await routines.ResumeAsync(household.HouseholdId, id, ct)))
            .Produces<TaskRoutineDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/stop", async (Guid id, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            Found(await routines.StopAsync(household.HouseholdId, id, ct)))
            .Produces<TaskRoutineDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapGet("/{id:guid}/image", async (Guid id, HouseholdContext household, HttpContext context, LifeOSDbContext db,
            TaskImageStorage storage, CancellationToken ct) =>
        {
            var image = await db.TaskRoutines.Where(r => r.Id == id && r.HouseholdId == household.HouseholdId)
                .Select(r => new { r.ImagePath, r.ImageContentType }).SingleOrDefaultAsync(ct);
            return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
        });
        g.MapPut("/{id:guid}/image", async (Guid id, HttpRequest request, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
        {
            var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
            await using var content = file.OpenReadStream();
            return Found(await routines.ReplaceImageAsync(household.HouseholdId, id, content, file.Length, file.ContentType, ct));
        }).WithUploadLimit().Produces<TaskRoutineDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/image", async (Guid id, HouseholdContext household, TaskRoutineLibrary routines, CancellationToken ct) =>
            await routines.RemoveImageAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    private static IResult Found(TaskRoutineDetailDto? routine) => routine is null ? Results.NotFound() : Results.Ok(routine);
}

/// <summary>
/// The household's routines (ADR 0017), shared by the endpoints and the assistant tools; rules in
/// <see cref="TaskRoutine"/> and <see cref="RoutineSchedule"/>. A null result means "not in this household".
/// </summary>
public sealed class TaskRoutineLibrary(LifeOSDbContext db, TaskImageStorage images, IRemoteImageSource remoteImages,
    TaskProfiles profiles, TimeProvider clock, ILogger<TaskRoutineLibrary> logger)
{
    private DateOnly Today => TaskClock.Today(clock);

    /// <summary>
    /// Routines filtered by state (active and paused by default: <c>active</c>; <c>stopped</c>; <c>all</c>) and
    /// tag, searched like todos; by next due day, paused then stopped last.
    /// </summary>
    public async Task<List<TaskRoutineDto>> SearchAsync(Guid householdId, string? state, string? q, string? tag, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(state) ? "active" : state.Trim();
        if (code is not ("active" or "stopped" or "all")) throw new ArgumentException("État : active (actives et en pause), stopped ou all.");
        var query = db.TaskRoutines.Where(r => r.HouseholdId == householdId);
        if (code == "active") query = query.Where(r => r.State != "stopped");
        if (code == "stopped") query = query.Where(r => r.State == "stopped");
        var routines = await query.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(tag))
            routines = routines.Where(r => r.Tags.Contains(tag.Trim(), StringComparer.OrdinalIgnoreCase)).ToList();
        var words = TodoLibrary.Normalize(q ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
        {
            routines = routines.Where(r =>
            {
                var text = TodoLibrary.Normalize(string.Join(' ', new[] { r.Title, r.Notes ?? "" }.Concat(r.Tags)));
                return words.All(text.Contains);
            }).ToList();
        }
        var today = Today;
        return Ordered(routines.Select(r => TaskRoutineDto.From(r, today))).ToList();
    }

    /// <summary>Active routines due within a week (late ones included) or done today, for the "Aujourd'hui" page.</summary>
    public async Task<List<TaskRoutineDto>> DueSoonAsync(Guid householdId, CancellationToken ct)
    {
        var today = Today;
        var routines = await db.TaskRoutines.Where(r => r.HouseholdId == householdId && r.State == "active").ToListAsync(ct);
        return Ordered(routines.Select(r => TaskRoutineDto.From(r, today))
            .Where(r => r.DoneToday || r.NextDueOn <= today.AddDays(7))).ToList();
    }

    public async Task<TaskRoutineDetailDto?> GetAsync(Guid householdId, Guid id, CancellationToken ct) =>
        await FindAsync(householdId, id, ct) is { } routine ? TaskRoutineDetailDto.From(routine, Today) : null;

    public async Task<TaskRoutineDetailDto> CreateAsync(Guid householdId, TaskRoutineRequest r, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, r.MemberProfileId, ct);
        var routine = TaskRoutine.Create(householdId, profile, r.Title, r.Notes, r.DurationMinutes, r.Priority, r.Tags,
            r.Recurrence.ToRecurrence(), r.StartOn ?? Today, clock.GetUtcNow());
        db.TaskRoutines.Add(routine);
        await db.SaveChangesAsync(ct);
        return TaskRoutineDetailDto.From(routine, Today);
    }

    public Task<TaskRoutineDetailDto?> UpdateAsync(Guid householdId, Guid id, TaskRoutineRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, routine => routine.Update(r.Title, r.Notes, r.DurationMinutes, r.Priority, r.Tags,
            r.Recurrence.ToRecurrence(), r.StartOn ?? routine.StartOn, clock.GetUtcNow()), ct);

    public Task<TaskRoutineDetailDto?> CompleteAsync(Guid householdId, Guid id, DateOnly? day, string? note, CancellationToken ct) =>
        ChangeAsync(householdId, id, routine => routine.Complete(day ?? Today, note, Today, clock.GetUtcNow()), ct);

    public Task<TaskRoutineDetailDto?> SkipAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, routine => routine.Skip(Today, clock.GetUtcNow()), ct);

    public Task<TaskRoutineDetailDto?> RemovePassAsync(Guid householdId, Guid id, Guid passId, CancellationToken ct) =>
        ChangeAsync(householdId, id, routine => routine.RemovePass(passId, clock.GetUtcNow()), ct);

    public Task<TaskRoutineDetailDto?> PauseAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, routine => routine.Pause(clock.GetUtcNow()), ct);

    public Task<TaskRoutineDetailDto?> ResumeAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, routine => routine.Resume(Today, clock.GetUtcNow()), ct);

    public Task<TaskRoutineDetailDto?> StopAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, routine => routine.Stop(clock.GetUtcNow()), ct);

    public async Task<TaskRoutineDetailDto?> ReplaceImageAsync(Guid householdId, Guid id, Stream content, long length, string? contentType,
        CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        await images.ReplaceImageAsync(content, length, contentType, routine.ImagePath,
            stored => routine.SetImage(stored.FileName, stored.ContentType), () => db.SaveChangesAsync(ct), logger, ct);
        return TaskRoutineDetailDto.From(routine, Today);
    }

    /// <summary>Downloads an image found by the assistant, then stores it like an upload.</summary>
    public async Task<TaskRoutineDetailDto?> ReplaceImageFromUrlAsync(Guid householdId, Guid id, string url, CancellationToken ct)
    {
        if (await FindAsync(householdId, id, ct) is null) return null;
        var image = await remoteImages.DownloadAsync(url, ct);
        using var content = new MemoryStream(image.Bytes);
        return await ReplaceImageAsync(householdId, id, content, image.Bytes.Length, image.ContentType, ct);
    }

    public async Task<TaskRoutineDetailDto?> RemoveImageAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        var oldPath = routine.ImagePath;
        routine.RemoveImage();
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove deleted routine image"));
        return TaskRoutineDetailDto.From(routine, Today);
    }

    public async Task<List<string>> TagsAsync(Guid householdId, CancellationToken ct) =>
        (await db.TaskRoutines.Where(r => r.HouseholdId == householdId).Select(r => r.Tags).ToListAsync(ct)).SelectMany(t => t).ToList();

    private async Task<TaskRoutineDetailDto?> ChangeAsync(Guid householdId, Guid id, Action<TaskRoutine> change, CancellationToken ct)
    {
        var routine = await FindAsync(householdId, id, ct);
        if (routine is null) return null;
        change(routine);
        await db.SaveChangesAsync(ct);
        return TaskRoutineDetailDto.From(routine, Today);
    }

    private Task<TaskRoutine?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.TaskRoutines.SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId, ct);

    private static IEnumerable<TaskRoutineDto> Ordered(IEnumerable<TaskRoutineDto> routines) => routines
        .OrderBy(r => r.State == "active" ? 0 : r.State == "paused" ? 1 : 2)
        .ThenBy(r => r.NextDueOn ?? DateOnly.MaxValue)
        .ThenBy(r => r.Priority == "high" ? 0 : r.Priority == "normal" ? 1 : 2)
        .ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase);
}
