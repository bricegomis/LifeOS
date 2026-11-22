using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Health;
using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Health;
using LifeOS.Infrastructure.Health;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Health;

public sealed record SupplementLinkRequest([property: StringLength(100)] string? Label, [property: Required, StringLength(SupplementLink.MaxUrlLength)] string Url);

/// <param name="Kind">year (all year) or cure.</param>
/// <param name="StartOn">From this day; today when omitted.</param>
/// <param name="CureUnit">week or month (a week is 7 days, a month 30).</param>
/// <param name="PauseLength">Pause before the cure starts again; 0 or none for a single cure.</param>
public sealed record SupplementPeriodRequest(string Kind, DateOnly? StartOn, int? CureLength, string? CureUnit, int? PauseLength, string? PauseUnit)
{
    public SupplementPeriod ToPeriod(DateOnly today) => Kind?.Trim() switch
    {
        "year" => SupplementPeriod.AllYear(StartOn ?? today),
        "cure" => SupplementPeriod.Cure(StartOn ?? today, CureLength ?? 0, CureUnit ?? "month", PauseLength ?? 0, PauseUnit ?? "month"),
        _ => throw new ArgumentException("Période : year (à l'année) ou cure."),
    };
}

/// <param name="Dose">Daily dose, in <paramref name="Unit"/>.</param>
/// <param name="Unit">gelule, comprime, g, ml, goutte or dose.</param>
/// <param name="Moment">matin, midi, soir or choix (default).</param>
/// <param name="PackageContent">Contents of one package, in the same unit as the dose.</param>
/// <param name="PackagePrice">Price paid for one package, in euros.</param>
public sealed record SupplementRequest(
    [property: Required, StringLength(Supplement.MaxNameLength)] string Name,
    [property: StringLength(Supplement.MaxNotesLength)] string? Notes,
    decimal Dose,
    [property: Required] string Unit,
    string? Moment,
    decimal PackageContent,
    decimal? PackagePrice,
    [property: StringLength(200)] string? Shop,
    IReadOnlyList<SupplementLinkRequest>? Links,
    [property: Required] SupplementPeriodRequest Period);

/// <param name="Day">Today when omitted; never in the future.</param>
public sealed record SupplementIntakeRequest(DateOnly? Day, bool Taken = true);

public static class SupplementsEndpoints
{
    public static IEndpointRouteBuilder MapSupplementsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/health/supplements").RequireAuthorization().WithTags("Santé").AddRequestValidation();
        g.MapGet("/", async (bool? includeStopped, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
            Results.Ok(await supplements.ListAsync(household, includeStopped ?? false, ct)))
            .Produces<SupplementListDto>();
        g.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
            Found(await supplements.GetAsync(household, id, ct)))
            .Produces<SupplementDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/", async (SupplementRequest request, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
        {
            var supplement = await supplements.CreateAsync(household, request, ct);
            return Results.Created($"/api/health/supplements/{supplement.Id}", supplement);
        }).Produces<SupplementDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        g.MapPut("/{id:guid}", async (Guid id, SupplementRequest request, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
            Found(await supplements.UpdateAsync(household, id, request, ct)))
            .Produces<SupplementDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/intakes", async (Guid id, SupplementIntakeRequest request, HouseholdContext household, SupplementLibrary supplements,
            CancellationToken ct) => Found(await supplements.SetTakenAsync(household, id, request.Day, request.Taken, ct)))
            .Produces<SupplementDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/stop", async (Guid id, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
            Found(await supplements.StopAsync(household, id, ct)))
            .Produces<SupplementDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:guid}/resume", async (Guid id, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
            Found(await supplements.ResumeAsync(household, id, ct)))
            .Produces<SupplementDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapGet("/{id:guid}/image", async (Guid id, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
            await supplements.ImageAsync(household, id, ct) is { } image ? Results.File(image.Bytes, image.ContentType) : Results.NotFound());
        g.MapPut("/{id:guid}/image", async (Guid id, HttpRequest request, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
        {
            var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
            await using var content = file.OpenReadStream();
            return Found(await supplements.ReplaceImageAsync(household, id, content, file.Length, file.ContentType, ct));
        }).WithUploadLimit().Produces<SupplementDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:guid}/image", async (Guid id, HouseholdContext household, SupplementLibrary supplements, CancellationToken ct) =>
            await supplements.RemoveImageAsync(household, id, ct) is null ? Results.NotFound() : Results.NoContent())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    private static IResult Found(SupplementDto? supplement) => supplement is null ? Results.NotFound() : Results.Ok(supplement);
}

/// <summary>A decrypted health file.</summary>
public sealed record HealthFile(byte[] Bytes, string ContentType);

/// <summary>
/// The caller's supplements (ADR 0018), shared by the endpoints and the assistant tools; rules in
/// <see cref="Supplement"/>. A null result means "not one of the caller's supplements".
/// </summary>
public sealed class SupplementLibrary(LifeOSDbContext db, HealthOwners owners, HealthFileStorage files, TimeProvider clock,
    ILogger<SupplementLibrary> logger)
{
    private DateOnly Today => HealthClock.Today(clock);

    public async Task<SupplementListDto> ListAsync(HouseholdContext household, bool includeStopped, CancellationToken ct)
    {
        var today = Today;
        var supplements = await Mine(household).Where(s => includeStopped || !s.IsStopped).ToListAsync(ct);
        var dtos = supplements
            .OrderBy(s => s.IsStopped)
            .ThenBy(s => Array.IndexOf(Supplement.Moments.Keys.ToArray(), s.Moment))
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => SupplementDto.From(s, today)).ToList();
        return new SupplementListDto(today, dtos.Where(s => !s.IsStopped).Sum(s => s.MonthlyCost ?? 0), dtos);
    }

    public async Task<SupplementDto?> GetAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        await FindAsync(household, id, ct) is { } supplement ? SupplementDto.From(supplement, Today) : null;

    public async Task<SupplementDto> CreateAsync(HouseholdContext household, SupplementRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var supplement = Supplement.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Name, r.Notes, r.Dose, r.Unit, r.Moment,
            r.PackageContent, r.PackagePrice, r.Shop, Links(r), r.Period.ToPeriod(Today), clock.GetUtcNow());
        db.Supplements.Add(supplement);
        await db.SaveChangesAsync(ct);
        return SupplementDto.From(supplement, Today);
    }

    public Task<SupplementDto?> UpdateAsync(HouseholdContext household, Guid id, SupplementRequest r, CancellationToken ct) =>
        ChangeAsync(household, id, s => s.Update(r.Name, r.Notes, r.Dose, r.Unit, r.Moment, r.PackageContent, r.PackagePrice, r.Shop,
            Links(r), r.Period.ToPeriod(Today), clock.GetUtcNow()), ct);

    public Task<SupplementDto?> SetTakenAsync(HouseholdContext household, Guid id, DateOnly? day, bool taken, CancellationToken ct) =>
        ChangeAsync(household, id, s => s.SetTaken(day ?? Today, taken, Today, clock.GetUtcNow()), ct);

    public Task<SupplementDto?> StopAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        ChangeAsync(household, id, s => s.Stop(clock.GetUtcNow()), ct);

    public Task<SupplementDto?> ResumeAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        ChangeAsync(household, id, s => s.Resume(clock.GetUtcNow()), ct);

    public async Task<HealthFile?> ImageAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var image = await Mine(household).Where(s => s.Id == id).Select(s => new { s.ImagePath, s.ImageContentType }).SingleOrDefaultAsync(ct);
        if (image?.ImagePath is null || image.ImageContentType is null || !files.Exists(image.ImagePath)) return null;
        return new HealthFile(await files.ReadAsync(image.ImagePath, ct), image.ImageContentType);
    }

    public async Task<SupplementDto?> ReplaceImageAsync(HouseholdContext household, Guid id, Stream content, long length, string? contentType,
        CancellationToken ct)
    {
        var supplement = await FindAsync(household, id, ct);
        if (supplement is null) return null;
        var stored = await files.SaveAsync(content, length, contentType, allowPdf: false, ct);
        var old = supplement.ImagePath;
        try
        {
            supplement.SetImage(stored.FileName, stored.ContentType);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            files.Delete(stored.FileName);
            throw;
        }
        files.DeleteQuietly(old, e => logger.LogError(e, "Unable to remove a replaced supplement image"));
        return SupplementDto.From(supplement, Today);
    }

    public async Task<SupplementDto?> RemoveImageAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var supplement = await FindAsync(household, id, ct);
        if (supplement is null) return null;
        var old = supplement.ImagePath;
        supplement.RemoveImage();
        await db.SaveChangesAsync(ct);
        files.DeleteQuietly(old, e => logger.LogError(e, "Unable to remove a deleted supplement image"));
        return SupplementDto.From(supplement, Today);
    }

    private static IEnumerable<(string? Label, string Url)> Links(SupplementRequest r) => (r.Links ?? []).Select(l => (l.Label, l.Url));

    private async Task<SupplementDto?> ChangeAsync(HouseholdContext household, Guid id, Action<Supplement> change, CancellationToken ct)
    {
        var supplement = await FindAsync(household, id, ct);
        if (supplement is null) return null;
        change(supplement);
        await db.SaveChangesAsync(ct);
        return SupplementDto.From(supplement, Today);
    }

    /// <summary>The caller's own rows: same household and same account.</summary>
    private IQueryable<Supplement> Mine(HouseholdContext household) =>
        db.Supplements.Where(s => s.HouseholdId == household.HouseholdId && s.OwnerUserId == household.UserId);

    private Task<Supplement?> FindAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        Mine(household).SingleOrDefaultAsync(s => s.Id == id, ct);
}
