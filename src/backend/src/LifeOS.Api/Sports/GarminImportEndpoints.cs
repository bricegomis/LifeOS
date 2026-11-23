using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LifeOS.Api.Authentication;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Application.Sports;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

/// <summary>One Garmin activity as the garmin-sync service reads it, in Garmin's units.</summary>
/// <param name="StartedAt">Start instant (Garmin's startTimeGMT).</param>
/// <param name="DurationSeconds">Timer time, pauses excluded.</param>
/// <param name="ElapsedSeconds">Start to end; the duration when missing.</param>
/// <param name="DirectWorkoutRpe">Perceived effort as Garmin stores it, 10 to 100.</param>
/// <param name="DirectWorkoutFeel">Feeling as Garmin stores it, 0 to 100.</param>
/// <param name="HeartRateZoneSeconds">Seconds in zones 1 to 5.</param>
public sealed record GarminActivityRequest(
    [property: Required, StringLength(60)] string Type,
    [property: StringLength(500)] string? Name,
    DateTimeOffset StartedAt,
    [property: Range(1, 86400)] double DurationSeconds,
    [property: Range(1, 86400)] double? ElapsedSeconds,
    [property: Range(0, 1_000_000)] double? DistanceMeters,
    [property: Range(0, 20000)] double? ElevationGainMeters,
    [property: Range(0, 20000)] double? Calories,
    double? AverageHeartRate,
    double? MaxHeartRate,
    [property: Range(0, 100)] double? AverageSpeedMetersPerSecond,
    [property: Range(0, 5)] double? AerobicTrainingEffect,
    [property: Range(0, 5)] double? AnaerobicTrainingEffect,
    int? DirectWorkoutRpe,
    int? DirectWorkoutFeel,
    double[]? HeartRateZoneSeconds,
    JsonElement? Raw);

public sealed record GarminReconcileRequest([property: Required] List<long> ActivityIds);

public sealed record GarminReconcileDto(int Deleted);

/// <param name="Sport">running or cycling.</param>
public sealed record Vo2MaxInput(DateOnly Date, [property: Required] string Sport, [property: Range(10, 100)] decimal Value);

public sealed record Vo2MaxImportRequest([property: Required] List<Vo2MaxInput> Measures);

public sealed record Vo2MaxDto(DateOnly Date, string Sport, string SportLabel, decimal Value)
{
    public static Vo2MaxDto From(Vo2MaxMeasure m) => new(m.Date, m.Sport, Vo2MaxMeasure.Sports[m.Sport], m.Value);
}

/// <summary>
/// What garmin-sync writes into the Sport module (ADR 0019), with an import token only. LifeOS never
/// calls Garmin: the service reads it and sends activities and VO2max here.
/// </summary>
public static class GarminImportEndpoints
{
    public static IEndpointRouteBuilder MapGarminImportEndpoints(this IEndpointRouteBuilder app)
    {
        var import = app.MapGroup("/api/import/garmin").RequireAuthorization(ImportTokenAuthentication.Policy).WithTags("Import").AddRequestValidation();
        import.MapPut("/activities/{activityId:long}", async (long activityId, GarminActivityRequest request, HttpContext http,
                HouseholdContext household, GarminSportImport importer, CancellationToken ct) =>
            Results.Ok(await importer.UpsertActivityAsync(household.HouseholdId, http.User.ImportProfile(), activityId, request, ct)))
            .Produces<SportSessionDto>().ProducesValidationProblem();
        import.MapPost("/activities/reconcile", async (GarminReconcileRequest request, HttpContext http, HouseholdContext household,
                GarminSportImport importer, CancellationToken ct) =>
            Results.Ok(await importer.ReconcileAsync(household.HouseholdId, http.User.ImportProfile(), request.ActivityIds, ct)))
            .Produces<GarminReconcileDto>().ProducesValidationProblem();
        import.MapPost("/vo2max", async (Vo2MaxImportRequest request, HttpContext http, HouseholdContext household,
                GarminSportImport importer, CancellationToken ct) =>
        {
            await importer.UpsertVo2MaxAsync(household.HouseholdId, http.User.ImportProfile(), request.Measures, ct);
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent).ProducesValidationProblem();

        app.MapGet("/api/sport/vo2max", async (DateOnly? from, DateOnly? to, Guid? profileId, HouseholdContext household,
                GarminSportImport importer, CancellationToken ct) =>
            Results.Ok(await importer.Vo2MaxAsync(household.HouseholdId, profileId, from, to, ct)))
            .RequireAuthorization().WithTags("Sport").Produces<List<Vo2MaxDto>>();
        return app;
    }
}

/// <summary>Garmin data into the Sport module: activities become sessions, VO2max is kept per day and sport.</summary>
public sealed class GarminSportImport(LifeOSDbContext db, SportProfiles profiles, TimeProvider clock)
{
    public async Task<SportSessionDto> UpsertActivityAsync(Guid householdId, Guid profileId, long activityId, GarminActivityRequest r,
        CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, profileId, ct);
        var values = Values(activityId, r);
        var today = SportClock.Today(clock);
        var now = clock.GetUtcNow();
        var session = await db.SportSessions.SingleOrDefaultAsync(s => s.HouseholdId == householdId && s.MemberProfileId == profile
            && s.GarminActivityId == activityId, ct);
        if (session is null)
        {
            var from = values.Date.AddDays(-1);
            var to = values.Date.AddDays(1);
            var candidates = await db.SportSessions.Where(s => s.HouseholdId == householdId && s.MemberProfileId == profile
                && s.Source == SportSession.ManualSource && s.Date >= from && s.Date <= to).ToListAsync(ct);
            session = candidates.OrderBy(s => s.StartedAt).FirstOrDefault(s => s.MatchesGarmin(values.Sport, values.StartedAt, values.EndedAt));
        }
        if (session is null)
        {
            session = SportSession.FromGarmin(householdId, profile, values, today, now);
            db.SportSessions.Add(session);
        }
        else
        {
            session.ApplyGarmin(values, today, now);
        }
        await db.SaveChangesAsync(ct);
        var names = await db.SportTemplates.AsNoTracking().Where(t => t.HouseholdId == householdId).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        return SportSessionDto.From(session, names);
    }

    /// <summary>Deletes the Garmin sessions whose activity is no longer in Garmin Connect (notes included).</summary>
    public async Task<GarminReconcileDto> ReconcileAsync(Guid householdId, Guid profileId, IReadOnlyCollection<long> activityIds, CancellationToken ct)
    {
        if (activityIds.Count == 0) throw new ArgumentException("Liste d'activités vide : rien n'est supprimé par sécurité.");
        var profile = await profiles.ResolveAsync(householdId, profileId, ct);
        var keep = activityIds.ToHashSet();
        var gone = (await db.SportSessions.Where(s => s.HouseholdId == householdId && s.MemberProfileId == profile
                && s.Source == SportSession.GarminSource).ToListAsync(ct))
            .Where(s => s.GarminActivityId is { } id && !keep.Contains(id)).ToList();
        db.SportSessions.RemoveRange(gone);
        await db.SaveChangesAsync(ct);
        return new GarminReconcileDto(gone.Count);
    }

    public async Task UpsertVo2MaxAsync(Guid householdId, Guid profileId, IReadOnlyList<Vo2MaxInput> measures, CancellationToken ct)
    {
        if (measures.Count > 2000) throw new ArgumentException("2000 mesures au plus par envoi.");
        var profile = await profiles.ResolveAsync(householdId, profileId, ct);
        var today = SportClock.Today(clock);
        var dates = measures.Select(m => m.Date).ToList();
        var existing = await db.Vo2MaxMeasures.Where(m => m.HouseholdId == householdId && m.MemberProfileId == profile && dates.Contains(m.Date))
            .ToListAsync(ct);
        foreach (var input in measures)
        {
            var sport = input.Sport.Trim().ToLowerInvariant();
            var measure = existing.FirstOrDefault(m => m.Date == input.Date && m.Sport == sport);
            if (measure is null)
            {
                measure = Vo2MaxMeasure.Create(householdId, profile, input.Date, sport, input.Value, today);
                db.Vo2MaxMeasures.Add(measure);
                existing.Add(measure);
            }
            else
            {
                measure.Set(input.Date, sport, input.Value, today);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Vo2MaxDto>> Vo2MaxAsync(Guid householdId, Guid? profileId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var profile = await profiles.ResolveAsync(householdId, profileId, ct);
        var end = to ?? SportClock.Today(clock);
        var start = from ?? end.AddYears(-2);
        if (end < start || end.DayNumber - start.DayNumber > 3660) throw new ArgumentException("Période invalide.");
        return (await db.Vo2MaxMeasures.AsNoTracking()
                .Where(m => m.HouseholdId == householdId && m.MemberProfileId == profile && m.Date >= start && m.Date <= end)
                .OrderBy(m => m.Date).ThenBy(m => m.Sport).ToListAsync(ct))
            .Select(Vo2MaxDto.From).ToList();
    }

    private static GarminActivityValues Values(long activityId, GarminActivityRequest r)
    {
        if (activityId <= 0) throw new ArgumentException("Identifiant d'activité Garmin invalide.");
        var sport = GarminActivityMapping.SportFor(r.Type);
        var start = r.StartedAt.ToUniversalTime();
        var end = start.AddSeconds(Math.Max(r.ElapsedSeconds ?? r.DurationSeconds, r.DurationSeconds));
        static int? Int(double? x) => x is { } v ? (int)Math.Round(v) : null;
        var hr = Int(r.AverageHeartRate) is >= 30 and <= 250 ? Int(r.AverageHeartRate) : null;
        var hrMax = Int(r.MaxHeartRate) is >= 30 and <= 250 ? Int(r.MaxHeartRate) : null;
        if (hr > hrMax) hr = null;
        var km = SportCatalog.HasDistance(sport) && r.DistanceMeters is > 0 ? Math.Round((decimal)r.DistanceMeters.Value / 1000m, 3) : (decimal?)null;
        var zones = r.HeartRateZoneSeconds is { Length: 5 } z ? z.Select(x => (int)Math.Round(Math.Max(0, x))).ToArray() : null;
        return new GarminActivityValues(activityId, r.Type.Trim(), r.Name, sport, SportClock.DayOf(start), start, end,
            Math.Max(1, (int)Math.Round(r.DurationSeconds / 60)), km, Int(r.Calories), GarminActivityMapping.Effort(r.DirectWorkoutRpe),
            GarminActivityMapping.Feel(r.DirectWorkoutFeel), hr, hrMax, Int(r.ElevationGainMeters),
            r.AverageSpeedMetersPerSecond is > 0 ? (decimal)r.AverageSpeedMetersPerSecond.Value * 3.6m : null,
            r.AerobicTrainingEffect is { } te ? (decimal)te : null, r.AnaerobicTrainingEffect is { } an ? (decimal)an : null,
            zones, r.Raw is { ValueKind: JsonValueKind.Object } raw ? raw.GetRawText() : null);
    }
}
