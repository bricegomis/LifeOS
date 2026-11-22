using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using LifeOS.Api.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Infrastructure.Files;
using LifeOS.Application.Tasks;
using LifeOS.Application.Tasks.Contracts;
using LifeOS.Domain.Tasks;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Tasks;

/// <param name="MemberProfileId">The main profile when omitted. Ignored on update.</param>
/// <param name="DueOn">Due day; none means "Sans échéance". Postponing is an update with a new due day.</param>
/// <param name="DurationMinutes">Estimated duration, 1 to 1440 minutes.</param>
/// <param name="Priority">high, normal (default) or low.</param>
public sealed record TodoRequest(
    Guid? MemberProfileId,
    [property: Required, StringLength(Todo.MaxTitleLength)] string Title,
    [property: StringLength(Todo.MaxNotesLength)] string? Notes,
    DateOnly? DueOn,
    [property: Range(1, Todo.MaxDurationMinutes)] int? DurationMinutes,
    string? Priority,
    IReadOnlyList<string>? Tags);

/// <param name="DoneOn">Day it was done; today when omitted, never in the future.</param>
public sealed record TodoCompleteRequest(DateOnly? DoneOn);

public sealed record ChecklistItemRequest([property: Required, StringLength(ChecklistItem.MaxLabelLength)] string Label, bool Done = false);

public static class TodosEndpoints
{
    public static IEndpointRouteBuilder MapTodosEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tasks").RequireAuthorization().WithTags("Tâches").AddRequestValidation();
        g.MapGet("/today", async (HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            Results.Ok(await todos.TodayAsync(household.HouseholdId, ct)))
            .Produces<TaskTodayDto>();
        g.MapGet("/tags", async (HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            Results.Ok(await todos.TagsAsync(household.HouseholdId, ct)))
            .Produces<List<string>>();
        g.MapGet("/todos", async (string? state, string? q, string? tag, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            Results.Ok(await todos.SearchAsync(household.HouseholdId, state, q, tag, ct)))
            .Produces<List<TodoDto>>().ProducesValidationProblem();
        g.MapGet("/todos/{id:guid}", async (Guid id, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            await todos.GetAsync(household.HouseholdId, id, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/todos", async (TodoRequest request, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
        {
            var todo = await todos.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/tasks/todos/{todo.Id}", todo);
        }).Produces<TodoDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/todos/{id:guid}", async (Guid id, TodoRequest request, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            await todos.UpdateAsync(household.HouseholdId, id, request, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/todos/{id:guid}/complete", async (Guid id, TodoCompleteRequest? request, HouseholdContext household, TodoLibrary todos,
            CancellationToken ct) =>
            await todos.CompleteAsync(household.HouseholdId, id, request?.DoneOn, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/todos/{id:guid}/reopen", async (Guid id, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            await todos.ReopenAsync(household.HouseholdId, id, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/todos/{id:guid}/abandon", async (Guid id, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            await todos.AbandonAsync(household.HouseholdId, id, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/todos/{id:guid}/checklist", async (Guid id, ChecklistItemRequest request, HouseholdContext household, TodoLibrary todos,
            CancellationToken ct) =>
            await todos.AddChecklistItemAsync(household.HouseholdId, id, request, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPut("/todos/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, ChecklistItemRequest request,
            HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            await todos.UpdateChecklistItemAsync(household.HouseholdId, id, itemId, request, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/todos/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, HouseholdContext household, TodoLibrary todos,
            CancellationToken ct) =>
            await todos.RemoveChecklistItemAsync(household.HouseholdId, id, itemId, ct) is { } todo ? Results.Ok(todo) : Results.NotFound())
            .Produces<TodoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapGet("/todos/{id:guid}/image", async (Guid id, HouseholdContext household, HttpContext context, LifeOSDbContext db,
            TaskImageStorage storage, CancellationToken ct) =>
        {
            var image = await db.Todos.Where(t => t.Id == id && t.HouseholdId == household.HouseholdId)
                .Select(t => new { t.ImagePath, t.ImageContentType }).SingleOrDefaultAsync(ct);
            return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
        });
        g.MapPut("/todos/{id:guid}/image", async (Guid id, HttpRequest request, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
        {
            var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
            await using var content = file.OpenReadStream();
            return await todos.ReplaceImageAsync(household.HouseholdId, id, content, file.Length, file.ContentType, ct) is { } todo
                ? Results.Ok(todo) : Results.NotFound();
        }).WithUploadLimit().Produces<TodoDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/todos/{id:guid}/image", async (Guid id, HouseholdContext household, TodoLibrary todos, CancellationToken ct) =>
            await todos.RemoveImageAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>
/// The household's todos (ADR 0017), shared by the endpoints and the assistant tools; rules in
/// <see cref="Todo"/>. A null result means "not in this household".
/// </summary>
public sealed class TodoLibrary(LifeOSDbContext db, TaskImageStorage images, IRemoteImageSource remoteImages,
    TaskProfiles profiles, TaskRoutineLibrary routines, TimeProvider clock, ILogger<TodoLibrary> logger)
{
    private DateOnly Today => TaskClock.Today(clock);

    public async Task<TaskTodayDto> TodayAsync(Guid householdId, CancellationToken ct)
    {
        var today = Today;
        var horizon = today.AddDays(7);
        var todos = await Query(householdId)
            .Where(t => (t.State == "open" && (t.DueOn == null || t.DueOn <= horizon)) || (t.State == "done" && t.DoneOn == today))
            .ToListAsync(ct);
        return new TaskTodayDto(today, Ordered(todos).Select(t => TodoDto.From(t, today)).ToList(), await routines.DueSoonAsync(householdId, ct));
    }

    /// <summary>
    /// The "Toutes les tâches" list: filtered by state (open by default, done, abandoned or all) and tag,
    /// searched in title, notes, tags and sub-tasks, ignoring case and accents; every word must match.
    /// </summary>
    public async Task<List<TodoDto>> SearchAsync(Guid householdId, string? state, string? q, string? tag, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(state) ? "open" : state.Trim();
        if (code != "all" && !Todo.States.ContainsKey(code)) throw new ArgumentException("État : open, done, abandoned ou all.");
        var query = Query(householdId);
        if (code != "all") query = query.Where(t => t.State == code);
        var todos = await query.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(tag))
            todos = todos.Where(t => t.Tags.Contains(tag.Trim(), StringComparer.OrdinalIgnoreCase)).ToList();
        var words = Normalize(q ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
        {
            todos = todos.Where(t =>
            {
                var text = Normalize(string.Join(' ', new[] { t.Title, t.Notes ?? "" }.Concat(t.Tags).Concat(t.Checklist.Select(i => i.Label))));
                return words.All(text.Contains);
            }).ToList();
        }
        var today = Today;
        return Ordered(todos).Select(t => TodoDto.From(t, today)).ToList();
    }

    /// <summary>The tags used by the household's todos and routines, to suggest them.</summary>
    public async Task<List<string>> TagsAsync(Guid householdId, CancellationToken ct)
    {
        var tags = await db.Todos.Where(t => t.HouseholdId == householdId).Select(t => t.Tags).ToListAsync(ct);
        return tags.SelectMany(t => t).Concat(await routines.TagsAsync(householdId, ct))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<TodoDto?> GetAsync(Guid householdId, Guid id, CancellationToken ct) =>
        await FindAsync(householdId, id, ct) is { } todo ? TodoDto.From(todo, Today) : null;

    public async Task<TodoDto> CreateAsync(Guid householdId, TodoRequest r, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, r.MemberProfileId, ct);
        var todo = Todo.Create(householdId, profile, r.Title, r.Notes, r.DueOn, r.DurationMinutes, r.Priority, r.Tags, clock.GetUtcNow());
        db.Todos.Add(todo);
        await db.SaveChangesAsync(ct);
        return TodoDto.From(todo, Today);
    }

    public Task<TodoDto?> UpdateAsync(Guid householdId, Guid id, TodoRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, t => t.Update(r.Title, r.Notes, r.DueOn, r.DurationMinutes, r.Priority, r.Tags, clock.GetUtcNow()), ct);

    public Task<TodoDto?> CompleteAsync(Guid householdId, Guid id, DateOnly? doneOn, CancellationToken ct) =>
        ChangeAsync(householdId, id, t => t.Complete(doneOn ?? Today, Today, clock.GetUtcNow()), ct);

    public Task<TodoDto?> ReopenAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, t => t.Reopen(clock.GetUtcNow()), ct);

    public Task<TodoDto?> AbandonAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, t => t.Abandon(clock.GetUtcNow()), ct);

    public Task<TodoDto?> AddChecklistItemAsync(Guid householdId, Guid id, ChecklistItemRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, t =>
        {
            var item = t.AddChecklistItem(r.Label, clock.GetUtcNow());
            if (r.Done) t.UpdateChecklistItem(item.Id, r.Label, true, clock.GetUtcNow());
        }, ct);

    public Task<TodoDto?> UpdateChecklistItemAsync(Guid householdId, Guid id, Guid itemId, ChecklistItemRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, t => t.UpdateChecklistItem(itemId, r.Label, r.Done, clock.GetUtcNow()), ct);

    public Task<TodoDto?> RemoveChecklistItemAsync(Guid householdId, Guid id, Guid itemId, CancellationToken ct) =>
        ChangeAsync(householdId, id, t => t.RemoveChecklistItem(itemId, clock.GetUtcNow()), ct);

    public async Task<TodoDto?> ReplaceImageAsync(Guid householdId, Guid id, Stream content, long length, string? contentType,
        CancellationToken ct)
    {
        var todo = await FindAsync(householdId, id, ct);
        if (todo is null) return null;
        await images.ReplaceImageAsync(content, length, contentType, todo.ImagePath,
            stored => todo.SetImage(stored.FileName, stored.ContentType), () => db.SaveChangesAsync(ct), logger, ct);
        return TodoDto.From(todo, Today);
    }

    /// <summary>Downloads an image found by the assistant, then stores it like an upload.</summary>
    public async Task<TodoDto?> ReplaceImageFromUrlAsync(Guid householdId, Guid id, string url, CancellationToken ct)
    {
        if (await FindAsync(householdId, id, ct) is null) return null;
        var image = await remoteImages.DownloadAsync(url, ct);
        using var content = new MemoryStream(image.Bytes);
        return await ReplaceImageAsync(householdId, id, content, image.Bytes.Length, image.ContentType, ct);
    }

    public async Task<TodoDto?> RemoveImageAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var todo = await FindAsync(householdId, id, ct);
        if (todo is null) return null;
        var oldPath = todo.ImagePath;
        todo.RemoveImage();
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove deleted todo image"));
        return TodoDto.From(todo, Today);
    }

    private async Task<TodoDto?> ChangeAsync(Guid householdId, Guid id, Action<Todo> change, CancellationToken ct)
    {
        var todo = await FindAsync(householdId, id, ct);
        if (todo is null) return null;
        change(todo);
        await db.SaveChangesAsync(ct);
        return TodoDto.From(todo, Today);
    }

    private IQueryable<Todo> Query(Guid householdId) => db.Todos.Where(t => t.HouseholdId == householdId);

    private Task<Todo?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.Todos.SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == householdId, ct);

    /// <summary>Open first, by due day (none last), high priority first, then title.</summary>
    private static IEnumerable<Todo> Ordered(IEnumerable<Todo> todos) => todos
        .OrderBy(t => t.IsOpen ? 0 : 1)
        .ThenBy(t => t.DueOn ?? DateOnly.MaxValue)
        .ThenBy(t => t.Priority == "high" ? 0 : t.Priority == "normal" ? 1 : 2)
        .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Lower case, without accents, apostrophes and punctuation as spaces.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return builder.ToString();
    }
}
