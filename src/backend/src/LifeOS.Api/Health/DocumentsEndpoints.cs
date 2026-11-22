using System.Globalization;
using System.Text;
using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Health;
using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Health;
using LifeOS.Infrastructure.Health;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Health;

/// <param name="Type">ordonnance, compte_rendu, resultat, bilan or autre.</param>
/// <param name="Day">The document's date; today when omitted.</param>
/// <param name="Who">Doctor, speciality or laboratory.</param>
public sealed record HealthDocumentRequest([property: Required] string Type, [property: Required, StringLength(200)] string Title, DateOnly? Day,
    [property: StringLength(200)] string? Who, [property: StringLength(HealthDocument.MaxNotesLength)] string? Notes);

/// <param name="Time">Time of the appointment, when known.</param>
/// <param name="BookingUrl">The doctor's booking page (Doctolib or another site), https.</param>
/// <param name="ReportDocumentId">The report, a health document of the same account, after the appointment.</param>
public sealed record AppointmentRequest(DateOnly Day, TimeOnly? Time, [property: Required, StringLength(200)] string Who, [property: StringLength(300)] string? Reason,
    [property: StringLength(300)] string? Place, [property: StringLength(2000)] string? Notes, [property: StringLength(1000)] string? BookingUrl, Guid? ReportDocumentId);

public static class DocumentsEndpoints
{
    public const long MaxUploadBytes = 21 * 1024 * 1024;

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var docs = app.MapGroup("/api/health/documents").RequireAuthorization().WithTags("Santé").AddRequestValidation();
        docs.MapGet("/", async (string? type, string? q, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
            Results.Ok(await library.ListAsync(household, type, q, ct))).Produces<List<HealthDocumentDto>>();
        docs.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
            await library.GetAsync(household, id, ct) is { } doc ? Results.Ok(doc) : Results.NotFound())
            .Produces<HealthDocumentDto>().ProducesProblem(StatusCodes.Status404NotFound);
        docs.MapPost("/", async (HealthDocumentRequest request, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
            Results.Ok(await library.CreateAsync(household, request, ct))).Produces<HealthDocumentDto>().ProducesValidationProblem();
        docs.MapPut("/{id:guid}", async (Guid id, HealthDocumentRequest request, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
            await library.UpdateAsync(household, id, request, ct) is { } doc ? Results.Ok(doc) : Results.NotFound())
            .Produces<HealthDocumentDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        docs.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
            await library.RemoveAsync(household, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        docs.MapPost("/{id:guid}/files", async (Guid id, HttpRequest request, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? throw new ArgumentException("Sélectionnez une photo ou un PDF.");
            await using var content = file.OpenReadStream();
            return await library.AddFileAsync(household, id, content, file.Length, file.ContentType, file.FileName, ct) is { } doc ? Results.Ok(doc) : Results.NotFound();
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxUploadBytes),
            new Microsoft.AspNetCore.Mvc.RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxUploadBytes })
            .Produces<HealthDocumentDto>().ProducesProblem(StatusCodes.Status404NotFound);
        docs.MapGet("/{id:guid}/files/{fileId:guid}", async (Guid id, Guid fileId, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
            await library.FileAsync(household, id, fileId, ct) is { } file ? Results.File(file.Bytes, file.ContentType) : Results.NotFound());
        docs.MapDelete("/{id:guid}/files/{fileId:guid}", async (Guid id, Guid fileId, HouseholdContext household, HealthDocumentLibrary library, CancellationToken ct) =>
            await library.RemoveFileAsync(household, id, fileId, ct) is { } doc ? Results.Ok(doc) : Results.NotFound())
            .Produces<HealthDocumentDto>().ProducesProblem(StatusCodes.Status404NotFound);

        var appointments = app.MapGroup("/api/health/appointments").RequireAuthorization().WithTags("Santé").AddRequestValidation();
        appointments.MapGet("/", async (HouseholdContext household, AppointmentLibrary library, CancellationToken ct) =>
            Results.Ok(await library.ListAsync(household, ct))).Produces<AppointmentListDto>();
        appointments.MapPost("/", async (AppointmentRequest request, HouseholdContext household, AppointmentLibrary library, CancellationToken ct) =>
            Results.Ok(await library.CreateAsync(household, request, ct))).Produces<AppointmentDto>().ProducesValidationProblem();
        appointments.MapPut("/{id:guid}", async (Guid id, AppointmentRequest request, HouseholdContext household, AppointmentLibrary library, CancellationToken ct) =>
            await library.UpdateAsync(household, id, request, ct) is { } appointment ? Results.Ok(appointment) : Results.NotFound())
            .Produces<AppointmentDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        appointments.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, AppointmentLibrary library, CancellationToken ct) =>
            await library.RemoveAsync(household, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>
/// The caller's health documents (ADR 0018), shared by the endpoints and the assistant tools. Files
/// are stored encrypted; the API never reads their content. Null or false means "not the caller's".
/// </summary>
public sealed class HealthDocumentLibrary(LifeOSDbContext db, HealthOwners owners, HealthFileStorage files, TimeProvider clock,
    ILogger<HealthDocumentLibrary> logger)
{
    private DateOnly Today => HealthClock.Today(clock);

    /// <summary>Newest first, filtered by type and searched in title, doctor and notes (case and accents ignored).</summary>
    public async Task<List<HealthDocumentDto>> ListAsync(HouseholdContext household, string? type, string? q, CancellationToken ct)
    {
        var documents = await Mine(household).Where(d => string.IsNullOrEmpty(type) || d.Type == type).ToListAsync(ct);
        var words = Normalize(q ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0) documents = documents.Where(d => words.All(Normalize($"{d.Title} {d.Who} {d.Notes}").Contains)).ToList();
        var links = await LinksAsync(household, ct);
        return documents.OrderByDescending(d => d.Day).ThenByDescending(d => d.CreatedAt)
            .Select(d => HealthDocumentDto.From(d, links.Where(l => l.DocumentId == d.Id).Select(l => l.Id))).ToList();
    }

    public async Task<HealthDocumentDto?> GetAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        await Mine(household).SingleOrDefaultAsync(d => d.Id == id, ct) is { } document ? await DtoAsync(household, document, ct) : null;

    public async Task<HealthDocumentDto> CreateAsync(HouseholdContext household, HealthDocumentRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var document = HealthDocument.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Type, r.Title, r.Day ?? Today, r.Who, r.Notes, Today, clock.GetUtcNow());
        db.HealthDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return HealthDocumentDto.From(document, []);
    }

    public async Task<HealthDocumentDto?> UpdateAsync(HouseholdContext household, Guid id, HealthDocumentRequest r, CancellationToken ct)
    {
        var document = await Mine(household).SingleOrDefaultAsync(d => d.Id == id, ct);
        if (document is null) return null;
        document.Update(r.Type, r.Title, r.Day ?? document.Day, r.Who, r.Notes, Today);
        await db.SaveChangesAsync(ct);
        return await DtoAsync(household, document, ct);
    }

    /// <summary>Deletes a document and its files; analyses and appointments that pointed to it keep their data.</summary>
    public async Task<bool> RemoveAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var document = await Mine(household).SingleOrDefaultAsync(d => d.Id == id, ct);
        if (document is null) return false;
        var stored = document.Files.Select(f => f.StoredName).ToList();
        db.HealthDocuments.Remove(document);
        await db.SaveChangesAsync(ct);
        foreach (var name in stored) files.DeleteQuietly(name, e => logger.LogError(e, "Unable to remove a deleted health file"));
        return true;
    }

    public async Task<HealthDocumentDto?> AddFileAsync(HouseholdContext household, Guid id, Stream content, long length, string? contentType, string? name,
        CancellationToken ct)
    {
        var document = await Mine(household).SingleOrDefaultAsync(d => d.Id == id, ct);
        if (document is null) return null;
        if (document.Files.Count >= HealthDocument.MaxFiles) throw new ArgumentException($"Un document compte au plus {HealthDocument.MaxFiles} fichiers.");
        var stored = await files.SaveAsync(content, length, contentType, allowPdf: true, ct);
        try
        {
            document.AddFile(stored.FileName, stored.ContentType, length, name);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            files.Delete(stored.FileName);
            throw;
        }
        return await DtoAsync(household, document, ct);
    }

    public async Task<HealthFile?> FileAsync(HouseholdContext household, Guid id, Guid fileId, CancellationToken ct)
    {
        var document = await Mine(household).SingleOrDefaultAsync(d => d.Id == id, ct);
        var file = document?.Files.SingleOrDefault(f => f.Id == fileId);
        if (file is null || !files.Exists(file.StoredName)) return null;
        return new HealthFile(await files.ReadAsync(file.StoredName, ct), file.ContentType);
    }

    public async Task<HealthDocumentDto?> RemoveFileAsync(HouseholdContext household, Guid id, Guid fileId, CancellationToken ct)
    {
        var document = await Mine(household).SingleOrDefaultAsync(d => d.Id == id, ct);
        if (document is null || document.Files.All(f => f.Id != fileId)) return null;
        var removed = document.RemoveFile(fileId);
        await db.SaveChangesAsync(ct);
        files.DeleteQuietly(removed.StoredName, e => logger.LogError(e, "Unable to remove a health file"));
        return await DtoAsync(household, document, ct);
    }

    private async Task<HealthDocumentDto> DtoAsync(HouseholdContext household, HealthDocument document, CancellationToken ct) =>
        HealthDocumentDto.From(document, await db.Analyses.Where(a => a.HouseholdId == household.HouseholdId && a.OwnerUserId == household.UserId && a.DocumentId == document.Id)
            .Select(a => a.Id).ToListAsync(ct));

    private async Task<List<(Guid Id, Guid? DocumentId)>> LinksAsync(HouseholdContext household, CancellationToken ct) =>
        (await db.Analyses.Where(a => a.HouseholdId == household.HouseholdId && a.OwnerUserId == household.UserId && a.DocumentId != null)
            .Select(a => new { a.Id, a.DocumentId }).ToListAsync(ct)).Select(a => (a.Id, a.DocumentId)).ToList();

    private IQueryable<HealthDocument> Mine(HouseholdContext household) =>
        db.HealthDocuments.Where(d => d.HouseholdId == household.HouseholdId && d.OwnerUserId == household.UserId);

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return builder.ToString();
    }
}

/// <summary>The caller's medical appointments (ADR 0018), shared by the endpoints and the assistant tools.</summary>
public sealed class AppointmentLibrary(LifeOSDbContext db, HealthOwners owners, TimeProvider clock)
{
    private DateOnly Today => HealthClock.Today(clock);

    public async Task<AppointmentListDto> ListAsync(HouseholdContext household, CancellationToken ct)
    {
        var today = Today;
        var all = await Mine(household).ToListAsync(ct);
        var titles = await TitlesAsync(household, ct);
        var dtos = all.Select(a => Dto(a, titles)).ToList();
        return new AppointmentListDto(today,
            dtos.Where(a => a.Day >= today).OrderBy(a => a.Day).ThenBy(a => a.Time ?? TimeOnly.MaxValue).ToList(),
            dtos.Where(a => a.Day < today).OrderByDescending(a => a.Day).ThenByDescending(a => a.Time ?? TimeOnly.MinValue).ToList());
    }

    public async Task<AppointmentDto> CreateAsync(HouseholdContext household, AppointmentRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var appointment = Appointment.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Day, r.Time, r.Who, r.Reason, r.Place, r.Notes, r.BookingUrl);
        appointment.SetReport(await ReportAsync(household, r.ReportDocumentId, ct));
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        return Dto(appointment, await TitlesAsync(household, ct));
    }

    public async Task<AppointmentDto?> UpdateAsync(HouseholdContext household, Guid id, AppointmentRequest r, CancellationToken ct)
    {
        var appointment = await Mine(household).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (appointment is null) return null;
        appointment.Update(r.Day, r.Time, r.Who, r.Reason, r.Place, r.Notes, r.BookingUrl);
        appointment.SetReport(await ReportAsync(household, r.ReportDocumentId, ct));
        await db.SaveChangesAsync(ct);
        return Dto(appointment, await TitlesAsync(household, ct));
    }

    public async Task<bool> RemoveAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var appointment = await Mine(household).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (appointment is null) return false;
        db.Appointments.Remove(appointment);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Guid?> ReportAsync(HouseholdContext household, Guid? id, CancellationToken ct)
    {
        if (id is not { } documentId) return null;
        return await db.HealthDocuments.AnyAsync(d => d.Id == documentId && d.HouseholdId == household.HouseholdId && d.OwnerUserId == household.UserId, ct)
            ? documentId : throw new ArgumentException("Document introuvable.");
    }

    private Task<Dictionary<Guid, string>> TitlesAsync(HouseholdContext household, CancellationToken ct) =>
        db.HealthDocuments.Where(d => d.HouseholdId == household.HouseholdId && d.OwnerUserId == household.UserId).ToDictionaryAsync(d => d.Id, d => d.Title, ct);

    private static AppointmentDto Dto(Appointment a, IReadOnlyDictionary<Guid, string> titles) =>
        new(a.Id, a.Day, a.Time, a.Who, a.Reason, a.Place, a.Notes, a.BookingUrl, a.ReportDocumentId,
            a.ReportDocumentId is { } id && titles.TryGetValue(id, out var title) ? title : null);

    private IQueryable<Appointment> Mine(HouseholdContext household) =>
        db.Appointments.Where(a => a.HouseholdId == household.HouseholdId && a.OwnerUserId == household.UserId);
}
