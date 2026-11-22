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

public sealed record TaskProjectRequest(
    [property: Required, StringLength(Todo.MaxTitleLength)] string Title,
    [property: StringLength(Todo.MaxNotesLength)] string? Notes,
    DateOnly? DueOn);

public static class TaskProjectsEndpoints
{
    public static IEndpointRouteBuilder MapTaskProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tasks/projects").RequireAuthorization().WithTags("Tâches").AddRequestValidation();
        g.MapGet("/", async (string? state, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
            Results.Ok(await projects.ListAsync(household.HouseholdId, state, ct)))
            .Produces<List<TaskProjectDto>>().ProducesValidationProblem();
        g.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
            Found(await projects.GetAsync(household.HouseholdId, id, ct)))
            .Produces<TaskProjectDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/", async (TaskProjectRequest request, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
        {
            var project = await projects.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/tasks/projects/{project.Project.Id}", project);
        }).Produces<TaskProjectDetailDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, TaskProjectRequest request, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
            Found(await projects.UpdateAsync(household.HouseholdId, id, request, ct)))
            .Produces<TaskProjectDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/finish", async (Guid id, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
            Found(await projects.FinishAsync(household.HouseholdId, id, ct)))
            .Produces<TaskProjectDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/abandon", async (Guid id, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
            Found(await projects.AbandonAsync(household.HouseholdId, id, ct)))
            .Produces<TaskProjectDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/reopen", async (Guid id, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
            Found(await projects.ReopenAsync(household.HouseholdId, id, ct)))
            .Produces<TaskProjectDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapGet("/{id:guid}/image", async (Guid id, HouseholdContext household, HttpContext context, LifeOSDbContext db,
            TaskImageStorage storage, CancellationToken ct) =>
        {
            var image = await db.TaskProjects.Where(p => p.Id == id && p.HouseholdId == household.HouseholdId)
                .Select(p => new { p.ImagePath, p.ImageContentType }).SingleOrDefaultAsync(ct);
            return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
        });
        g.MapPut("/{id:guid}/image", async (Guid id, HttpRequest request, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
        {
            var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
            await using var content = file.OpenReadStream();
            return Found(await projects.ReplaceImageAsync(household.HouseholdId, id, content, file.Length, file.ContentType, ct));
        }).WithUploadLimit().Produces<TaskProjectDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/image", async (Guid id, HouseholdContext household, TaskProjectLibrary projects, CancellationToken ct) =>
            await projects.RemoveImageAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    private static IResult Found(TaskProjectDetailDto? project) => project is null ? Results.NotFound() : Results.Ok(project);
}

/// <summary>
/// The household's projects (ADR 0017), shared by the endpoints and the assistant tools; rules in
/// <see cref="TaskProject"/>. A null result means "not in this household".
/// </summary>
public sealed class TaskProjectLibrary(LifeOSDbContext db, TaskImageStorage images, IRemoteImageSource remoteImages,
    TodoLibrary todos, TimeProvider clock, ILogger<TaskProjectLibrary> logger)
{
    /// <summary>In progress by default (<c>active</c>), <c>closed</c> (done or abandoned) or <c>all</c>; by due day, then name.</summary>
    public async Task<List<TaskProjectDto>> ListAsync(Guid householdId, string? state, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(state) ? "active" : state.Trim();
        if (code is not ("active" or "closed" or "all")) throw new ArgumentException("État : active, closed ou all.");
        var query = db.TaskProjects.Where(p => p.HouseholdId == householdId);
        if (code == "active") query = query.Where(p => p.State == "active");
        if (code == "closed") query = query.Where(p => p.State != "active");
        var projects = await query.ToListAsync(ct);
        var ids = projects.Select(p => p.Id).ToList();
        var counted = await db.Todos.Where(t => t.HouseholdId == householdId && t.ProjectId != null && ids.Contains(t.ProjectId.Value)).ToListAsync(ct);
        return projects
            .OrderBy(p => p.IsActive ? 0 : 1)
            .ThenBy(p => p.DueOn ?? DateOnly.MaxValue)
            .ThenBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => TaskProjectDto.From(p, counted)).ToList();
    }

    public async Task<TaskProjectDetailDto?> GetAsync(Guid householdId, Guid id, CancellationToken ct) =>
        await FindAsync(householdId, id, ct) is { } project ? await DetailAsync(project, ct) : null;

    public async Task<TaskProjectDetailDto> CreateAsync(Guid householdId, TaskProjectRequest r, CancellationToken ct)
    {
        var project = TaskProject.Create(householdId, r.Title, r.Notes, r.DueOn, clock.GetUtcNow());
        db.TaskProjects.Add(project);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(project, ct);
    }

    public Task<TaskProjectDetailDto?> UpdateAsync(Guid householdId, Guid id, TaskProjectRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, p => p.Update(r.Title, r.Notes, r.DueOn, clock.GetUtcNow()), ct);

    /// <summary>Finished; its open todos stay open and keep the project.</summary>
    public Task<TaskProjectDetailDto?> FinishAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, p => p.Finish(clock.GetUtcNow()), ct);

    public Task<TaskProjectDetailDto?> AbandonAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, p => p.Abandon(clock.GetUtcNow()), ct);

    public Task<TaskProjectDetailDto?> ReopenAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, p => p.Reopen(clock.GetUtcNow()), ct);

    public async Task<TaskProjectDetailDto?> ReplaceImageAsync(Guid householdId, Guid id, Stream content, long length, string? contentType,
        CancellationToken ct)
    {
        var project = await FindAsync(householdId, id, ct);
        if (project is null) return null;
        await images.ReplaceImageAsync(content, length, contentType, project.ImagePath,
            stored => project.SetImage(stored.FileName, stored.ContentType), () => db.SaveChangesAsync(ct), logger, ct);
        return await DetailAsync(project, ct);
    }

    /// <summary>Downloads an image found by the assistant, then stores it like an upload.</summary>
    public async Task<TaskProjectDetailDto?> ReplaceImageFromUrlAsync(Guid householdId, Guid id, string url, CancellationToken ct)
    {
        if (await FindAsync(householdId, id, ct) is null) return null;
        var image = await remoteImages.DownloadAsync(url, ct);
        using var content = new MemoryStream(image.Bytes);
        return await ReplaceImageAsync(householdId, id, content, image.Bytes.Length, image.ContentType, ct);
    }

    public async Task<TaskProjectDetailDto?> RemoveImageAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var project = await FindAsync(householdId, id, ct);
        if (project is null) return null;
        var oldPath = project.ImagePath;
        project.RemoveImage();
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove deleted project image"));
        return await DetailAsync(project, ct);
    }

    private async Task<TaskProjectDetailDto?> ChangeAsync(Guid householdId, Guid id, Action<TaskProject> change, CancellationToken ct)
    {
        var project = await FindAsync(householdId, id, ct);
        if (project is null) return null;
        change(project);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(project, ct);
    }

    private async Task<TaskProjectDetailDto> DetailAsync(TaskProject project, CancellationToken ct)
    {
        var own = await db.Todos.Where(t => t.HouseholdId == project.HouseholdId && t.ProjectId == project.Id).ToListAsync(ct);
        return new TaskProjectDetailDto(TaskProjectDto.From(project, own), await todos.DtosAsync(project.HouseholdId, TodoLibrary.Ordered(own), ct));
    }

    private Task<TaskProject?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.TaskProjects.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == householdId, ct);
}
