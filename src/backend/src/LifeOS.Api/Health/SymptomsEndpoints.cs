using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Health;
using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Health;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Health;

/// <param name="Kind">physique (default) or mental.</param>
public sealed record SymptomTypeRequest([property: Required, StringLength(100)] string Name, string? Kind);

/// <param name="Day">Today when omitted; never in the future.</param>
/// <param name="Intensity">1 (light) to 10 (very strong).</param>
public sealed record SymptomEntryRequest(Guid SymptomTypeId, DateOnly? Day, TimeOnly? Time, [property: Range(1, 10)] int Intensity,
    [property: StringLength(SymptomEntry.MaxNoteLength)] string? Note);

public sealed record CheckinSymptomRequest(Guid SymptomTypeId, [property: Range(1, 10)] int Intensity);

/// <param name="Calm">"Rien à signaler": the day counts as noted, without symptoms.</param>
/// <param name="Symptoms">The symptoms felt, when not calm.</param>
public sealed record CheckinRequest(DateOnly? Day, bool Calm, IReadOnlyList<CheckinSymptomRequest>? Symptoms);

public static class SymptomsEndpoints
{
    public static IEndpointRouteBuilder MapSymptomsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/health/symptoms").RequireAuthorization().WithTags("Santé").AddRequestValidation();
        g.MapGet("/", async (int? days, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            Results.Ok(await symptoms.JournalAsync(household, days ?? 30, ct)))
            .Produces<SymptomJournalDto>();
        g.MapPost("/types", async (SymptomTypeRequest request, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            Results.Ok(await symptoms.CreateTypeAsync(household, request, ct)))
            .Produces<SymptomTypeDto>().ProducesValidationProblem();
        g.MapPut("/types/{id:guid}", async (Guid id, SymptomTypeRequest request, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            await symptoms.UpdateTypeAsync(household, id, request, null, ct) is { } type ? Results.Ok(type) : Results.NotFound())
            .Produces<SymptomTypeDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/types/{id:guid}/archive", async (Guid id, bool? restore, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            await symptoms.UpdateTypeAsync(household, id, null, !(restore ?? false), ct) is { } type ? Results.Ok(type) : Results.NotFound())
            .Produces<SymptomTypeDto>().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/entries", async (SymptomEntryRequest request, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            Results.Ok(await symptoms.AddEntryAsync(household, request, ct)))
            .Produces<SymptomEntryDto>().ProducesValidationProblem();
        g.MapPut("/entries/{id:guid}", async (Guid id, SymptomEntryRequest request, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            await symptoms.UpdateEntryAsync(household, id, request, ct) is { } entry ? Results.Ok(entry) : Results.NotFound())
            .Produces<SymptomEntryDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/entries/{id:guid}", async (Guid id, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            await symptoms.RemoveEntryAsync(household, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/checkin", async (CheckinRequest request, HouseholdContext household, SymptomLibrary symptoms, CancellationToken ct) =>
            Results.Ok(await symptoms.CheckinAsync(household, request, ct)))
            .Produces<SymptomDayDto>().ProducesValidationProblem();
        return app;
    }
}

/// <summary>
/// The caller's symptom journal (ADR 0018), shared by the endpoints and the assistant tools: a list
/// of symptoms (seeded with the defaults the first time), entries at any time, and the daily check-in
/// where "rien à signaler" counts as a noted day. A null or false result means "not the caller's".
/// </summary>
public sealed class SymptomLibrary(LifeOSDbContext db, HealthOwners owners, TimeProvider clock)
{
    private DateOnly Today => HealthClock.Today(clock);

    public async Task<SymptomJournalDto> JournalAsync(HouseholdContext household, int days, CancellationToken ct)
    {
        if (days is < 1 or > 3660) throw new ArgumentException("Période : de 1 à 3660 jours.");
        var today = Today;
        var from = today.AddDays(1 - days);
        var types = await TypesAsync(household, ct);
        var entries = await db.SymptomEntries.Where(e => e.HouseholdId == household.HouseholdId && e.OwnerUserId == household.UserId && e.Day >= from)
            .ToListAsync(ct);
        var calm = await db.CalmDays.Where(d => d.HouseholdId == household.HouseholdId && d.OwnerUserId == household.UserId && d.Day >= from)
            .Select(d => d.Day).ToListAsync(ct);
        var byId = types.ToDictionary(t => t.Id);
        var dayList = entries.Select(e => e.Day).Concat(calm).Distinct().OrderDescending()
            .Select(day =>
            {
                var dayEntries = entries.Where(e => e.Day == day).OrderByDescending(e => e.Time ?? TimeOnly.MaxValue).ThenByDescending(e => e.CreatedAt)
                    .Select(e => Dto(e, byId)).ToList();
                return new SymptomDayDto(day, calm.Contains(day), dayEntries.Count == 0 ? null : dayEntries.Max(e => e.Intensity), dayEntries);
            }).ToList();
        return new SymptomJournalDto(today, from, dayList, types.OrderBy(t => t.IsArchived).ThenBy(t => t.Kind).ThenBy(t => t.Name)
            .Select(SymptomTypeDto.From).ToList());
    }

    public async Task<SymptomTypeDto> CreateTypeAsync(HouseholdContext household, SymptomTypeRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var type = SymptomType.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, r.Name, r.Kind);
        db.SymptomTypes.Add(type);
        await db.SaveChangesAsync(ct);
        return SymptomTypeDto.From(type);
    }

    /// <summary>Renames (<paramref name="r"/>) or archives / restores (<paramref name="archive"/>) a symptom.</summary>
    public async Task<SymptomTypeDto?> UpdateTypeAsync(HouseholdContext household, Guid id, SymptomTypeRequest? r, bool? archive, CancellationToken ct)
    {
        var type = await db.SymptomTypes.SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == household.HouseholdId && t.OwnerUserId == household.UserId, ct);
        if (type is null) return null;
        if (r is not null) type.Update(r.Name, r.Kind);
        if (archive == true) type.Archive();
        if (archive == false) type.Restore();
        await db.SaveChangesAsync(ct);
        return SymptomTypeDto.From(type);
    }

    public async Task<SymptomEntryDto> AddEntryAsync(HouseholdContext household, SymptomEntryRequest r, CancellationToken ct)
    {
        var type = await TypeAsync(household, r.SymptomTypeId, ct);
        var day = r.Day ?? Today;
        var entry = SymptomEntry.Create(type, day, r.Time, r.Intensity, r.Note, Today, clock.GetUtcNow());
        db.SymptomEntries.Add(entry);
        await ForgetCalmAsync(household, day, ct);
        await db.SaveChangesAsync(ct);
        return Dto(entry, new Dictionary<Guid, SymptomType> { [type.Id] = type });
    }

    public async Task<SymptomEntryDto?> UpdateEntryAsync(HouseholdContext household, Guid id, SymptomEntryRequest r, CancellationToken ct)
    {
        var entry = await db.SymptomEntries.SingleOrDefaultAsync(e => e.Id == id && e.HouseholdId == household.HouseholdId && e.OwnerUserId == household.UserId, ct);
        if (entry is null) return null;
        var type = await TypeAsync(household, r.SymptomTypeId, ct);
        var day = r.Day ?? entry.Day;
        entry.Update(type, day, r.Time, r.Intensity, r.Note, Today);
        await ForgetCalmAsync(household, day, ct);
        await db.SaveChangesAsync(ct);
        return Dto(entry, new Dictionary<Guid, SymptomType> { [type.Id] = type });
    }

    public async Task<bool> RemoveEntryAsync(HouseholdContext household, Guid id, CancellationToken ct)
    {
        var entry = await db.SymptomEntries.SingleOrDefaultAsync(e => e.Id == id && e.HouseholdId == household.HouseholdId && e.OwnerUserId == household.UserId, ct);
        if (entry is null) return false;
        db.SymptomEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// The daily check-in: either "rien à signaler" (refused when symptoms are already noted that day),
    /// or the symptoms felt with their intensity.
    /// </summary>
    public async Task<SymptomDayDto> CheckinAsync(HouseholdContext household, CheckinRequest r, CancellationToken ct)
    {
        var owner = await owners.ResolveAsync(household, ct);
        var day = r.Day ?? Today;
        var symptoms = r.Symptoms ?? [];
        if (r.Calm)
        {
            if (symptoms.Count > 0) throw new ArgumentException("« Rien à signaler » ne va pas avec des symptômes.");
            if (await db.SymptomEntries.AnyAsync(e => e.HouseholdId == household.HouseholdId && e.OwnerUserId == household.UserId && e.Day == day, ct))
                throw new ArgumentException("Des symptômes sont déjà notés ce jour-là.");
            if (!await db.CalmDays.AnyAsync(d => d.HouseholdId == household.HouseholdId && d.OwnerUserId == household.UserId && d.Day == day, ct))
                db.CalmDays.Add(CalmDay.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, day, Today));
        }
        else
        {
            if (symptoms.Count == 0) throw new ArgumentException("Cochez au moins un symptôme, ou « rien à signaler ».");
            if (symptoms.Select(s => s.SymptomTypeId).Distinct().Count() != symptoms.Count) throw new ArgumentException("Chaque symptôme une seule fois.");
            foreach (var symptom in symptoms)
            {
                var type = await TypeAsync(household, symptom.SymptomTypeId, ct);
                db.SymptomEntries.Add(SymptomEntry.Create(type, day, null, symptom.Intensity, null, Today, clock.GetUtcNow()));
            }
            await ForgetCalmAsync(household, day, ct);
        }
        await db.SaveChangesAsync(ct);
        return (await JournalAsync(household, Today.DayNumber - day.DayNumber + 1, ct)).Days.FirstOrDefault(d => d.Day == day)
            ?? new SymptomDayDto(day, false, null, []);
    }

    /// <summary>The caller's symptom list, created with the defaults the first time.</summary>
    private async Task<List<SymptomType>> TypesAsync(HouseholdContext household, CancellationToken ct)
    {
        var types = await db.SymptomTypes.Where(t => t.HouseholdId == household.HouseholdId && t.OwnerUserId == household.UserId).ToListAsync(ct);
        if (types.Count > 0) return types;
        var owner = await owners.ResolveAsync(household, ct);
        types = SymptomType.Defaults.Select(d => SymptomType.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, d.Name, d.Kind)).ToList();
        db.SymptomTypes.AddRange(types);
        await db.SaveChangesAsync(ct);
        return types;
    }

    private async Task<SymptomType> TypeAsync(HouseholdContext household, Guid id, CancellationToken ct) =>
        await db.SymptomTypes.SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == household.HouseholdId && t.OwnerUserId == household.UserId && !t.IsArchived, ct)
        ?? throw new ArgumentException("Symptôme introuvable dans ta liste.");

    private async Task ForgetCalmAsync(HouseholdContext household, DateOnly day, CancellationToken ct) =>
        db.CalmDays.RemoveRange(await db.CalmDays.Where(d => d.HouseholdId == household.HouseholdId && d.OwnerUserId == household.UserId && d.Day == day).ToListAsync(ct));

    private static SymptomEntryDto Dto(SymptomEntry e, IReadOnlyDictionary<Guid, SymptomType> types)
    {
        var type = types.TryGetValue(e.SymptomTypeId, out var t) ? t : null;
        return new SymptomEntryDto(e.Id, e.SymptomTypeId, type?.Name ?? "?", type?.Kind ?? "physique", e.Day, e.Time, e.Intensity, e.Note);
    }
}
