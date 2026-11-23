using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LifeOS.Api.Authentication;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Health;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Health;

/// <summary>One day of sleep and recovery as garmin-sync sends it (ADR 0019).</summary>
/// <param name="Date">The day: the night ends that morning.</param>
/// <param name="Raw">Garmin's answers of the day, one property per endpoint.</param>
public sealed record WellnessInput(
    DateOnly Date,
    DateTimeOffset? SleepStart, DateTimeOffset? SleepEnd, int? SleepSeconds, int? DeepSleepSeconds, int? LightSleepSeconds,
    int? RemSleepSeconds, int? AwakeSeconds, int? SleepScore, string? SleepQuality,
    int? RestingHeartRate, int? HrvNightAverage, int? HrvWeeklyAverage, int? HrvBaselineLow, int? HrvBaselineHigh, string? HrvStatus,
    decimal? Respiration, decimal? SpO2, int? BodyBatteryWake, int? BodyBatteryHigh, int? BodyBatteryLow, int? StressAverage, int? Steps,
    JsonElement? Raw);

public sealed record WellnessImportRequest([property: Required] List<WellnessInput> Days);

public sealed record WellnessDto(
    DateOnly Date, DateTimeOffset? SleepStart, DateTimeOffset? SleepEnd, int? SleepSeconds, int? DeepSleepSeconds, int? LightSleepSeconds,
    int? RemSleepSeconds, int? AwakeSeconds, int? SleepScore, string? SleepQuality, int? RestingHeartRate, int? HrvNightAverage,
    int? HrvWeeklyAverage, int? HrvBaselineLow, int? HrvBaselineHigh, string? HrvStatus, string? HrvStatusLabel, decimal? Respiration,
    decimal? SpO2, int? BodyBatteryWake, int? BodyBatteryHigh, int? BodyBatteryLow, int? StressAverage, int? Steps, DateTimeOffset UpdatedAt)
{
    public static WellnessDto From(DailyWellness w) => new(w.Date, w.SleepStart, w.SleepEnd, w.SleepSeconds, w.DeepSleepSeconds,
        w.LightSleepSeconds, w.RemSleepSeconds, w.AwakeSeconds, w.SleepScore, w.SleepQuality, w.RestingHeartRate, w.HrvNightAverage,
        w.HrvWeeklyAverage, w.HrvBaselineLow, w.HrvBaselineHigh, w.HrvStatus, w.HrvStatus is { } s ? DailyWellness.HrvStatuses[s] : null,
        w.Respiration, w.SpO2, w.BodyBatteryWake, w.BodyBatteryHigh, w.BodyBatteryLow, w.StressAverage, w.Steps, w.UpdatedAt);
}

/// <summary>Sleep and recovery from Garmin (ADR 0019): written by garmin-sync, read by its owner only.</summary>
public static class WellnessEndpoints
{
    public static IEndpointRouteBuilder MapWellnessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health/wellness", async (DateOnly? from, DateOnly? to, HouseholdContext household, WellnessLibrary wellness, CancellationToken ct) =>
                Results.Ok(await wellness.ListAsync(household, from, to, ct)))
            .RequireAuthorization().WithTags("Health").Produces<List<WellnessDto>>();
        app.MapGet("/api/health/wellness/{date}/raw", async (DateOnly date, HouseholdContext household, WellnessLibrary wellness, CancellationToken ct) =>
                await wellness.RawAsync(household, date, ct) is { } raw ? Results.Text(raw, "application/json") : Results.NotFound())
            .RequireAuthorization().WithTags("Health");
        var import = app.MapGroup("/api/import/garmin").RequireAuthorization(ImportTokenAuthentication.Policy).WithTags("Import").AddRequestValidation();
        import.MapPost("/wellness", async (WellnessImportRequest request, HouseholdContext household, WellnessLibrary wellness, CancellationToken ct) =>
            {
                await wellness.UpsertAsync(household, request.Days, ct);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent).ProducesValidationProblem();
        return app;
    }
}

public sealed class WellnessLibrary(LifeOSDbContext db, HealthOwners owners, TimeProvider clock)
{
    public async Task<List<WellnessDto>> ListAsync(HouseholdContext household, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var end = to ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1);
        var start = from ?? end.AddDays(-30);
        if (end < start || end.DayNumber - start.DayNumber > 3660) throw new ArgumentException("Période invalide.");
        return (await db.DailyWellness.AsNoTracking()
                .Where(w => w.HouseholdId == household.HouseholdId && w.OwnerUserId == household.UserId && w.Date >= start && w.Date <= end)
                .OrderBy(w => w.Date).ToListAsync(ct))
            .Select(WellnessDto.From).ToList();
    }

    public async Task<string?> RawAsync(HouseholdContext household, DateOnly date, CancellationToken ct) =>
        await db.DailyWellness.AsNoTracking()
            .Where(w => w.HouseholdId == household.HouseholdId && w.OwnerUserId == household.UserId && w.Date == date)
            .Select(w => w.Raw).SingleOrDefaultAsync(ct);

    public async Task UpsertAsync(HouseholdContext household, IReadOnlyList<WellnessInput> days, CancellationToken ct)
    {
        if (days.Count > 1000) throw new ArgumentException("1000 jours au plus par envoi.");
        var owner = await owners.ResolveAsync(household, ct);
        var dates = days.Select(d => d.Date).ToList();
        var existing = await db.DailyWellness.Where(w => w.HouseholdId == owner.HouseholdId && w.OwnerUserId == owner.UserId && dates.Contains(w.Date))
            .ToDictionaryAsync(w => w.Date, ct);
        var now = clock.GetUtcNow();
        foreach (var d in days)
        {
            if (!existing.TryGetValue(d.Date, out var day))
            {
                day = DailyWellness.Create(owner.HouseholdId, owner.UserId, owner.ProfileId, d.Date);
                db.DailyWellness.Add(day);
                existing[d.Date] = day;
            }
            day.Apply(new DailyWellnessValues(d.SleepStart, d.SleepEnd, d.SleepSeconds, d.DeepSleepSeconds, d.LightSleepSeconds,
                d.RemSleepSeconds, d.AwakeSeconds, d.SleepScore, d.SleepQuality, d.RestingHeartRate, d.HrvNightAverage, d.HrvWeeklyAverage,
                d.HrvBaselineLow, d.HrvBaselineHigh, d.HrvStatus, d.Respiration, d.SpO2, d.BodyBatteryWake, d.BodyBatteryHigh,
                d.BodyBatteryLow, d.StressAverage, d.Steps, d.Raw is { ValueKind: JsonValueKind.Object } raw ? raw.GetRawText() : null), now);
        }
        await db.SaveChangesAsync(ct);
    }
}
