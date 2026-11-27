using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Health;
using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Health;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Health;

/// <param name="FrequencyMonths">Months between two measures, 0 = once.</param>
/// <param name="Panels">Panel names, e.g. "Bilan fer".</param>
/// <param name="Description">What it measures and what it is used for.</param>
/// <param name="WhenLow">What a value below the range can mean.</param>
/// <param name="WhenHigh">What a value above the range can mean.</param>
public sealed record MarkerRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Required, StringLength(30)] string Unit,
    decimal? LabMin, decimal? LabMax, decimal? TargetMin, decimal? TargetMax,
    [property: Range(0, 120)] int FrequencyMonths,
    IReadOnlyList<string>? Panels,
    [property: StringLength(Marker.MaxTextLength)] string? Description = null,
    [property: StringLength(Marker.MaxTextLength)] string? WhenLow = null,
    [property: StringLength(Marker.MaxTextLength)] string? WhenHigh = null);

public sealed record AnalysisValueRequest(Guid MarkerId, decimal Value);

/// <param name="Day">Day of the sample; today when omitted.</param>
/// <param name="DocumentId">The report it was read from (a health document); none when omitted.</param>
public sealed record AnalysisRequest(DateOnly? Day, [property: StringLength(100)] string? Laboratory, [property: StringLength(Analysis.MaxNotesLength)] string? Notes,
    Guid? DocumentId, [property: Required] IReadOnlyList<AnalysisValueRequest> Values);

/// <param name="TargetMin">Optional personal target; leave both empty for what does not change (height…).</param>
public sealed record MeasureTypeRequest([property: Required, StringLength(100)] string Name, [property: Required, StringLength(30)] string Unit,
    decimal? TargetMin = null, decimal? TargetMax = null);

/// <param name="Day">Today when omitted.</param>
public sealed record MeasurementRequest(Guid MeasureTypeId, DateOnly? Day, decimal Value, [property: StringLength(500)] string? Note);

public static class AnalysesEndpoints
{
    public static IEndpointRouteBuilder MapAnalysesEndpoints(this IEndpointRouteBuilder app)
    {
        var markers = app.MapGroup("/api/health/markers").RequireAuthorization().WithTags("Santé").AddRequestValidation();
        markers.MapGet("/", async (bool? includeArchived, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            Results.Ok(await library.MarkersAsync(household, includeArchived ?? false, ct))).Produces<MarkerListDto>();
        markers.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.MarkerAsync(household, id, ct) is { } marker ? Results.Ok(marker) : Results.NotFound())
            .Produces<MarkerDto>().ProducesProblem(StatusCodes.Status404NotFound);
        markers.MapPost("/", async (MarkerRequest request, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            Results.Ok(await library.CreateMarkerAsync(household, request, ct))).Produces<MarkerDto>().ProducesValidationProblem();
        markers.MapPut("/{id:guid}", async (Guid id, MarkerRequest request, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.UpdateMarkerAsync(household, id, request, null, ct) is { } marker ? Results.Ok(marker) : Results.NotFound())
            .Produces<MarkerDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        markers.MapPost("/{id:guid}/archive", async (Guid id, bool? restore, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.UpdateMarkerAsync(household, id, null, !(restore ?? false), ct) is { } marker ? Results.Ok(marker) : Results.NotFound())
            .Produces<MarkerDto>().ProducesProblem(StatusCodes.Status404NotFound);

        var analyses = app.MapGroup("/api/health/analyses").RequireAuthorization().WithTags("Santé").AddRequestValidation();
        analyses.MapGet("/", async (HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            Results.Ok(await library.AnalysesAsync(household, ct))).Produces<List<AnalysisDto>>();
        analyses.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.AnalysisAsync(household, id, ct) is { } analysis ? Results.Ok(analysis) : Results.NotFound())
            .Produces<AnalysisDto>().ProducesProblem(StatusCodes.Status404NotFound);
        analyses.MapPost("/", async (AnalysisRequest request, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            Results.Ok(await library.CreateAnalysisAsync(household, request, ct))).Produces<AnalysisDto>().ProducesValidationProblem();
        analyses.MapPut("/{id:guid}", async (Guid id, AnalysisRequest request, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.UpdateAnalysisAsync(household, id, request, ct) is { } analysis ? Results.Ok(analysis) : Results.NotFound())
            .Produces<AnalysisDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        analyses.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.RemoveAnalysisAsync(household, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        var measures = app.MapGroup("/api/health/measures").RequireAuthorization().WithTags("Santé").AddRequestValidation();
        measures.MapGet("/", async (HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            Results.Ok(await library.MeasuresAsync(household, ct))).Produces<List<MeasureTypeDto>>();
        measures.MapPost("/types", async (MeasureTypeRequest request, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            Results.Ok(await library.CreateMeasureTypeAsync(household, request, ct))).Produces<MeasureTypeDto>().ProducesValidationProblem();
        measures.MapPut("/types/{id:guid}", async (Guid id, MeasureTypeRequest request, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.UpdateMeasureTypeAsync(household, id, request, ct) is { } type ? Results.Ok(type) : Results.NotFound())
            .Produces<MeasureTypeDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        measures.MapPost("/types/{id:guid}/archive", async (Guid id, bool? restore, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.ArchiveMeasureTypeAsync(household, id, !(restore ?? false), ct) is { } type ? Results.Ok(type) : Results.NotFound())
            .Produces<MeasureTypeDto>().ProducesProblem(StatusCodes.Status404NotFound);
        measures.MapPost("/values", async (MeasurementRequest request, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            Results.Ok(await library.AddMeasurementAsync(household, request, ct))).Produces<MeasureTypeDto>().ProducesValidationProblem();
        measures.MapDelete("/values/{id:guid}", async (Guid id, HouseholdContext household, AnalysisLibrary library, CancellationToken ct) =>
            await library.RemoveMeasurementAsync(household, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>
/// The caller's markers, analyses and body measures (ADR 0018), shared by the endpoints and the
/// assistant tools: positions, trends and due dates are computed on read. Null or false means "not the caller's".
/// </summary>
public sealed class AnalysisLibrary(LifeOSDbContext db, HealthOwners owners, TimeProvider clock)
{
    private DateOnly Today => HealthClock.Today(clock);

    public async Task<MarkerListDto> MarkersAsync(HouseholdContext household, bool includeArchived, CancellationToken ct)
    {
        var markers = await MyMarkers(household).Where(m => includeArchived || !m.IsArchived).ToListAsync(ct);
        var analyses = await MyAnalyses(household).ToListAsync(ct);
        var today = Today;
        var dtos = markers.Select(m => MarkerDto.From(m, analyses, today))
            .OrderBy(m => m.IsArchived).ThenByDescending(m => m.Late).ThenByDescending(m => m.Due).ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new MarkerListDto(today, dtos, markers.SelectMany(m => m.Panels).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    public async Task<MarkerDto?> MarkerAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var marker = await MyMarkers(household).SingleOrDefaultAsync(m => m.Id == id, ct);
        return marker is null ? null : MarkerDto.From(marker, await MyAnalyses(household).ToListAsync(ct), Today);
    }

    public async Task<MarkerDto> CreateMarkerAsync(HouseholdContext household, MarkerRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var marker = Marker.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Name, r.Unit, r.LabMin, r.LabMax, r.TargetMin, r.TargetMax,
            r.FrequencyMonths, r.Panels);
        marker.Describe(r.Description, r.WhenLow, r.WhenHigh);
        db.Markers.Add(marker);
        await db.SaveChangesAsync(ct);
        return MarkerDto.From(marker, [], Today);
    }

    /// <summary>Replaces a marker (<paramref name="r"/>) or archives / restores it (<paramref name="archive"/>).</summary>
    public async Task<MarkerDto?> UpdateMarkerAsync(HouseholdContext household, Guid id, MarkerRequest? r, bool? archive, CancellationToken ct)
    {
        var marker = await MyMarkers(household).SingleOrDefaultAsync(m => m.Id == id, ct);
        if (marker is null) return null;
        if (r is not null)
        {
            marker.Update(r.Name, r.Unit, r.LabMin, r.LabMax, r.TargetMin, r.TargetMax, r.FrequencyMonths, r.Panels);
            marker.Describe(r.Description, r.WhenLow, r.WhenHigh);
        }
        if (archive == true) marker.Archive();
        if (archive == false) marker.Restore();
        await db.SaveChangesAsync(ct);
        return MarkerDto.From(marker, await MyAnalyses(household).ToListAsync(ct), Today);
    }

    public async Task<List<AnalysisDto>> AnalysesAsync(HouseholdContext household, CancellationToken ct)
    {
        var markers = await MyMarkers(household).ToDictionaryAsync(m => m.Id, ct);
        return (await MyAnalyses(household).ToListAsync(ct)).OrderByDescending(a => a.Day).Select(a => AnalysisDto.From(a, markers)).ToList();
    }

    public async Task<AnalysisDto?> AnalysisAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var analysis = await MyAnalyses(household).SingleOrDefaultAsync(a => a.Id == id, ct);
        return analysis is null ? null : AnalysisDto.From(analysis, await MyMarkers(household).ToDictionaryAsync(m => m.Id, ct));
    }

    public async Task<AnalysisDto> CreateAnalysisAsync(HouseholdContext household, AnalysisRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var markers = await MyMarkers(household).ToDictionaryAsync(m => m.Id, ct);
        var analysis = Analysis.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Day ?? Today, r.Laboratory, r.Notes, Values(r, markers), Today);
        analysis.LinkDocument(await DocumentAsync(household, r.DocumentId, ct));
        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(ct);
        return AnalysisDto.From(analysis, markers);
    }

    public async Task<AnalysisDto?> UpdateAnalysisAsync(HouseholdContext household, Guid id, AnalysisRequest r, CancellationToken ct)
    {
        var analysis = await MyAnalyses(household).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (analysis is null) return null;
        var markers = await MyMarkers(household).ToDictionaryAsync(m => m.Id, ct);
        analysis.Update(r.Day ?? analysis.Day, r.Laboratory, r.Notes, Values(r, markers), Today);
        analysis.LinkDocument(await DocumentAsync(household, r.DocumentId, ct));
        await db.SaveChangesAsync(ct);
        return AnalysisDto.From(analysis, markers);
    }

    public async Task<bool> RemoveAnalysisAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var analysis = await MyAnalyses(household).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (analysis is null) return false;
        db.Analyses.Remove(analysis);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<MeasureTypeDto>> MeasuresAsync(HouseholdContext household, CancellationToken ct)
    {
        var types = await MyMeasureTypes(household).ToListAsync(ct);
        if (types.Count == 0)
        {
            var owner = await owners.ResolveAsync(household, ct);
            types = MeasureType.Defaults.Select(d => MeasureType.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, d.Name, d.Unit)).ToList();
            db.MeasureTypes.AddRange(types);
            await db.SaveChangesAsync(ct);
        }
        var values = await db.Measurements.Where(m => m.HouseholdId == household.HouseholdId && m.OwnerUserId == household.UserId).ToListAsync(ct);
        return types.OrderBy(t => t.IsArchived).ThenBy(t => t.Name).Select(t => MeasureDto(t, values)).ToList();
    }

    public async Task<MeasureTypeDto> CreateMeasureTypeAsync(HouseholdContext household, MeasureTypeRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var type = MeasureType.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Name, r.Unit, r.TargetMin, r.TargetMax);
        db.MeasureTypes.Add(type);
        await db.SaveChangesAsync(ct);
        return MeasureDto(type, []);
    }

    public async Task<MeasureTypeDto?> UpdateMeasureTypeAsync(HouseholdContext household, Guid id, MeasureTypeRequest r, CancellationToken ct)
    {
        var type = await MyMeasureTypes(household).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (type is null) return null;
        type.Update(r.Name, r.Unit, r.TargetMin, r.TargetMax);
        await db.SaveChangesAsync(ct);
        return MeasureDto(type, await db.Measurements.Where(m => m.MeasureTypeId == id && m.OwnerUserId == household.UserId).ToListAsync(ct));
    }

    public async Task<MeasureTypeDto?> ArchiveMeasureTypeAsync(HouseholdContext household, Guid id, bool archive, CancellationToken ct)
    {
        var type = await MyMeasureTypes(household).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (type is null) return null;
        if (archive) type.Archive(); else type.Restore();
        await db.SaveChangesAsync(ct);
        return MeasureDto(type, await db.Measurements.Where(m => m.MeasureTypeId == id && m.OwnerUserId == household.UserId).ToListAsync(ct));
    }

    public async Task<MeasureTypeDto> AddMeasurementAsync(HouseholdContext household, MeasurementRequest r, CancellationToken ct)
    {
        var type = await MyMeasureTypes(household).SingleOrDefaultAsync(t => t.Id == r.MeasureTypeId && !t.IsArchived, ct)
            ?? throw new ArgumentException("Mesure introuvable dans ta liste.");
        db.Measurements.Add(Measurement.Create(type, r.Day ?? Today, r.Value, r.Note, Today));
        await db.SaveChangesAsync(ct);
        return MeasureDto(type, await db.Measurements.Where(m => m.MeasureTypeId == type.Id && m.OwnerUserId == household.UserId).ToListAsync(ct));
    }

    public async Task<bool> RemoveMeasurementAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var measurement = await db.Measurements.SingleOrDefaultAsync(m => m.Id == id && m.HouseholdId == household.HouseholdId && m.OwnerUserId == household.UserId, ct);
        if (measurement is null) return false;
        db.Measurements.Remove(measurement);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static IEnumerable<(Marker, decimal)> Values(AnalysisRequest r, IReadOnlyDictionary<Guid, Marker> markers) =>
        r.Values.Select(v => (markers.TryGetValue(v.MarkerId, out var m) ? m : throw new ArgumentException("Marqueur introuvable dans ta liste."), v.Value));

    /// <summary>A document of the same account, once documents exist; refused otherwise.</summary>
    private async Task<Guid?> DocumentAsync(HouseholdContext household, Guid? id, CancellationToken ct)
    {
        if (id is not { } documentId) return null;
        return await DocumentExistsAsync(household, documentId, ct) ? documentId : throw new ArgumentException("Document introuvable.");
    }

    private Task<bool> DocumentExistsAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        db.HealthDocuments.AnyAsync(d => d.Id == id && d.HouseholdId == household.HouseholdId && d.OwnerUserId == household.UserId, ct);

    private static MeasureTypeDto MeasureDto(MeasureType t, IEnumerable<Measurement> values)
    {
        var list = values.Where(v => v.MeasureTypeId == t.Id).OrderBy(v => v.Day)
            .Select(v => new MeasurementDto(v.Id, v.Day, v.Value, v.Note, t.StatusOf(v.Value))).ToList();
        return new(t.Id, t.Name, t.Unit, t.TargetMin, t.TargetMax, t.IsArchived, list, list.LastOrDefault()?.Status);
    }

    private IQueryable<Marker> MyMarkers(HouseholdContext household) =>
        db.Markers.Where(m => m.HouseholdId == household.HouseholdId && m.OwnerUserId == household.UserId);

    private IQueryable<Analysis> MyAnalyses(HouseholdContext household) =>
        db.Analyses.Where(a => a.HouseholdId == household.HouseholdId && a.OwnerUserId == household.UserId);

    private IQueryable<MeasureType> MyMeasureTypes(HouseholdContext household) =>
        db.MeasureTypes.Where(t => t.HouseholdId == household.HouseholdId && t.OwnerUserId == household.UserId);
}
