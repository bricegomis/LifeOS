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

/// <param name="ParentId">A project of the household in progress (or the current parent, even closed), never the project
/// itself nor one of its sub-projects; none for a top project.</param>
public sealed record TaskProjectRequest(
    [property: Required, StringLength(Todo.MaxTitleLength)] string Title,
    [property: StringLength(Todo.MaxNotesLength)] string? Notes,
    DateOnly? DueOn,
    Guid? ParentId = null);

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
/// The household's projects and sub-projects (ADR 0017), shared by the endpoints and the assistant tools; rules in
/// <see cref="TaskProject"/>. A null result means "not in this household".
/// </summary>
public sealed class TaskProjectLibrary(LifeOSDbContext db, TaskImageStorage images, IRemoteImageSource remoteImages,
    TodoLibrary todos, TimeProvider clock, ILogger<TaskProjectLibrary> logger)
{
    /// <summary>
    /// In progress by default (<c>active</c>), <c>closed</c> (done or abandoned) or <c>all</c>; by due day, then name.
    /// Each project counts the todos of its whole sub-tree; the client builds the tree from <c>ParentId</c>.
    /// </summary>
    public async Task<List<TaskProjectDto>> ListAsync(Guid householdId, string? state, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(state) ? "active" : state.Trim();
        if (code is not ("active" or "closed" or "all")) throw new ArgumentException("État : active, closed ou all.");
        var tree = await TreeAsync(householdId, ct);
        return Sorted(tree.Projects.Where(p => code == "all" || (code == "active") == p.IsActive)).Select(tree.Dto).ToList();
    }

    public async Task<TaskProjectDetailDto?> GetAsync(Guid householdId, Guid id, CancellationToken ct) =>
        await FindAsync(householdId, id, ct) is { } project ? await DetailAsync(project, ct) : null;

    public async Task<TaskProjectDetailDto> CreateAsync(Guid householdId, TaskProjectRequest r, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var project = TaskProject.Create(householdId, r.Title, r.Notes, r.DueOn, now);
        project.MoveUnder(await ParentAsync(householdId, r.ParentId, null, ct), _ => null, now);
        db.TaskProjects.Add(project);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(project, ct);
    }

    public async Task<TaskProjectDetailDto?> UpdateAsync(Guid householdId, Guid id, TaskProjectRequest r, CancellationToken ct)
    {
        var project = await FindAsync(householdId, id, ct);
        if (project is null) return null;
        var now = clock.GetUtcNow();
        var parent = await ParentAsync(householdId, r.ParentId, project.ParentId, ct);
        var tree = await TreeAsync(householdId, ct);
        project.Update(r.Title, r.Notes, r.DueOn, now);
        project.MoveUnder(parent, tree.ParentOf, now);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(project, ct);
    }

    /// <summary>Finished; its open todos stay open and keep the project, its sub-projects keep their state.</summary>
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
        var tree = await TreeAsync(project.HouseholdId, ct);
        var own = tree.Todos.Where(t => t.ProjectId == project.Id);
        var ancestors = new List<TaskProjectRefDto>();
        for (var parent = project.ParentId; parent is { } id && tree.Find(id) is { } found; parent = found.ParentId)
            ancestors.Insert(0, new TaskProjectRefDto(found.Id, found.Title));
        return new TaskProjectDetailDto(tree.Dto(project), await todos.DtosAsync(project.HouseholdId, TodoLibrary.Ordered(own), ct),
            Sorted(tree.ChildrenOf(project.Id)).Select(tree.Dto).ToList(), ancestors);
    }

    /// <summary>A project of the household in progress, or the current parent (even closed); none when null.</summary>
    private async Task<Guid?> ParentAsync(Guid householdId, Guid? parentId, Guid? current, CancellationToken ct)
    {
        if (parentId is not { } id || id == Guid.Empty) return null;
        if (id == current) return id;
        return await db.TaskProjects.AnyAsync(p => p.Id == id && p.HouseholdId == householdId && p.State == "active", ct)
            ? id
            : throw new ArgumentException("Projet parent introuvable ou déjà clos dans votre foyer.");
    }

    private static IEnumerable<TaskProject> Sorted(IEnumerable<TaskProject> projects) => projects
        .OrderBy(p => p.IsActive ? 0 : 1)
        .ThenBy(p => p.DueOn ?? DateOnly.MaxValue)
        .ThenBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase);

    private async Task<ProjectTree> TreeAsync(Guid householdId, CancellationToken ct) => new(
        await db.TaskProjects.Where(p => p.HouseholdId == householdId).ToListAsync(ct),
        await db.Todos.Where(t => t.HouseholdId == householdId && t.ProjectId != null).ToListAsync(ct));

    /// <summary>The household's projects as a tree, with the todos of each sub-tree for progress.</summary>
    private sealed class ProjectTree(List<TaskProject> projects, List<Todo> todos)
    {
        private readonly Dictionary<Guid, TaskProject> byId = projects.ToDictionary(p => p.Id);
        private readonly ILookup<Guid?, TaskProject> children = projects.ToLookup(p => p.ParentId);
        private readonly ILookup<Guid, Todo> todosOf = todos.ToLookup(t => t.ProjectId!.Value);

        public IReadOnlyList<TaskProject> Projects => projects;
        public IReadOnlyList<Todo> Todos => todos;
        public TaskProject? Find(Guid id) => byId.GetValueOrDefault(id);
        public Guid? ParentOf(Guid id) => Find(id)?.ParentId;
        public IEnumerable<TaskProject> ChildrenOf(Guid id) => children[id];

        public TaskProjectDto Dto(TaskProject project) =>
            TaskProjectDto.From(project, SubTree(project.Id).SelectMany(id => todosOf[id]), ChildrenOf(project.Id).Count(c => c.IsActive));

        private IEnumerable<Guid> SubTree(Guid id)
        {
            var seen = new HashSet<Guid>();
            var stack = new Stack<Guid>([id]);
            while (stack.TryPop(out var current))
            {
                if (!seen.Add(current)) continue;
                yield return current;
                foreach (var child in children[current]) stack.Push(child.Id);
            }
        }
    }

    private Task<TaskProject?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.TaskProjects.SingleOrDefaultAsync(p => p.Id == id && p.HouseholdId == householdId, ct);
}
