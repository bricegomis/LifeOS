using System.ComponentModel.DataAnnotations;
using LifeOS.Api.Authentication;
using LifeOS.Api.Validation;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Households;

public sealed record ImportTokenDto(Guid Id, string Name, string Hint, Guid MemberProfileId, DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt)
{
    public static ImportTokenDto From(ImportToken t) => new(t.Id, t.Name, t.Hint, t.MemberProfileId, t.CreatedAt, t.LastUsedAt);
}

/// <param name="Token">The secret, shown once: copy it into LIFEOS_IMPORT_TOKEN.</param>
public sealed record CreatedImportTokenDto(ImportTokenDto Info, string Token);

public sealed record ImportTokenRequest([property: StringLength(100)] string? Name, Guid? MemberProfileId);

/// <param name="State">never, ok, mfa_requested, code_refused, code_expired, credentials_refused or error.</param>
/// <param name="WaitsForCode">Garmin sent a code that the user can type.</param>
public sealed record GarminStatusDto(string State, string StateLabel, string? Message, bool WaitsForCode,
    DateTimeOffset? LastSeenAt, DateTimeOffset? LastSuccessAt, string? LastSuccessSummary, DateTimeOffset? LastErrorAt, string? LastError,
    DateTimeOffset? NextSyncAt, bool SyncRequested, bool ReconnectRequested, bool CodePending)
{
    public static GarminStatusDto From(GarminLink? l, DateTimeOffset now)
    {
        if (l is null) return new("never", GarminLink.States["never"], null, false, null, null, null, null, null, null, false, false, false);
        var state = l.StateAt(now);
        return new(state, GarminLink.States[state], l.Message, l.WaitsForCode(now), l.LastSeenAt, l.LastSuccessAt, l.LastSuccessSummary,
            l.LastErrorAt, l.LastError, l.NextSyncAt, l.SyncRequestedAt is not null, l.ReconnectRequestedAt is not null, l.MfaCode is not null);
    }
}

public sealed record GarminCodeRequest([property: Required] string Code);

/// <param name="State">ok, mfa_requested, code_refused, code_expired, credentials_refused or error.</param>
/// <param name="Summary">With ok after a sync: what was imported, e.g. "2 activités, 1 nuit".</param>
public sealed record GarminReportRequest([property: Required] string State, string? Message, string? Summary, DateTimeOffset? NextSyncAt);

/// <summary>
/// Import tokens and the Garmin connection of the caller's account (ADR 0019). The household core
/// holds them because an import writes into several modules; the data itself goes through each
/// module's import endpoints.
/// </summary>
public static class GarminEndpoints
{
    public static IEndpointRouteBuilder MapGarminEndpoints(this IEndpointRouteBuilder app)
    {
        var tokens = app.MapGroup("/api/import-tokens").RequireAuthorization().WithTags("Import").AddRequestValidation();
        tokens.MapGet("/", async (HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
            Results.Ok((await db.ImportTokens.AsNoTracking()
                .Where(t => t.HouseholdId == household.HouseholdId && t.UserId == household.UserId && t.RevokedAt == null)
                .OrderBy(t => t.CreatedAt).ToListAsync(ct)).Select(ImportTokenDto.From).ToList()))
            .Produces<List<ImportTokenDto>>();
        tokens.MapPost("/", async (ImportTokenRequest request, HouseholdContext household, LifeOSDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var profile = request.MemberProfileId ?? await db.MemberProfiles.Where(p => p.HouseholdId == household.HouseholdId)
                .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
                ?? throw new ArgumentException("Aucun profil dans votre foyer.");
            if (!await db.MemberProfiles.AnyAsync(p => p.Id == profile && p.HouseholdId == household.HouseholdId, ct))
                throw new ArgumentException("Profil introuvable dans votre foyer.");
            var (token, secret) = ImportToken.Create(household.HouseholdId, household.UserId, profile, request.Name, clock.GetUtcNow());
            db.ImportTokens.Add(token);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/import-tokens/{token.Id}", new CreatedImportTokenDto(ImportTokenDto.From(token), secret));
        }).Produces<CreatedImportTokenDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        tokens.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, LifeOSDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var token = await db.ImportTokens.SingleOrDefaultAsync(t => t.Id == id && t.HouseholdId == household.HouseholdId && t.UserId == household.UserId, ct);
            if (token is null) return Results.NotFound();
            token.Revoke(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        var garmin = app.MapGroup("/api/garmin").RequireAuthorization().WithTags("Import").AddRequestValidation();
        garmin.MapGet("/", async (HouseholdContext household, GarminLinks links, TimeProvider clock, CancellationToken ct) =>
            Results.Ok(GarminStatusDto.From(await links.FindAsync(household, ct), clock.GetUtcNow()))).Produces<GarminStatusDto>();
        garmin.MapPost("/sync", async (HouseholdContext household, GarminLinks links, CancellationToken ct) =>
            Results.Ok(await links.ChangeAsync(household, (l, now) => l.RequestSync(now), ct))).Produces<GarminStatusDto>();
        garmin.MapPost("/reconnect", async (HouseholdContext household, GarminLinks links, CancellationToken ct) =>
            Results.Ok(await links.ChangeAsync(household, (l, now) => l.RequestReconnect(now), ct))).Produces<GarminStatusDto>();
        garmin.MapPost("/code", async (GarminCodeRequest request, HouseholdContext household, GarminLinks links, CancellationToken ct) =>
            Results.Ok(await links.ChangeAsync(household, (l, now) => l.SubmitCode(request.Code, now), ct)))
            .Produces<GarminStatusDto>().ProducesValidationProblem();

        var service = app.MapGroup("/api/import/garmin").RequireAuthorization(ImportTokenAuthentication.Policy).WithTags("Import").AddRequestValidation();
        service.MapPost("/commands", async (HouseholdContext household, GarminLinks links, CancellationToken ct) =>
            Results.Ok(await links.TakeCommandsAsync(household, ct))).Produces<GarminCommands>();
        service.MapPost("/status", async (GarminReportRequest request, HouseholdContext household, GarminLinks links, CancellationToken ct) =>
            Results.Ok(await links.ChangeAsync(household, (l, now) => l.Report(request.State, request.Message, request.Summary, request.NextSyncAt, now), ct)))
            .Produces<GarminStatusDto>().ProducesValidationProblem();
        return app;
    }
}

/// <summary>The caller's Garmin connection; created on first use. Shared by the endpoints and the assistant.</summary>
public sealed class GarminLinks(LifeOSDbContext db, TimeProvider clock)
{
    public Task<GarminLink?> FindAsync(HouseholdContext household, CancellationToken ct) =>
        db.GarminLinks.AsNoTracking().SingleOrDefaultAsync(l => l.HouseholdId == household.HouseholdId && l.UserId == household.UserId, ct);

    public async Task<GarminStatusDto> StatusAsync(HouseholdContext household, CancellationToken ct) =>
        GarminStatusDto.From(await FindAsync(household, ct), clock.GetUtcNow());

    public async Task<GarminStatusDto> ChangeAsync(HouseholdContext household, Action<GarminLink, DateTimeOffset> change, CancellationToken ct)
    {
        var link = await TrackedAsync(household, ct);
        var now = clock.GetUtcNow();
        change(link, now);
        await db.SaveChangesAsync(ct);
        return GarminStatusDto.From(link, now);
    }

    public async Task<GarminCommands> TakeCommandsAsync(HouseholdContext household, CancellationToken ct)
    {
        var link = await TrackedAsync(household, ct);
        var commands = link.TakeCommands(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return commands;
    }

    private async Task<GarminLink> TrackedAsync(HouseholdContext household, CancellationToken ct)
    {
        var link = await db.GarminLinks.SingleOrDefaultAsync(l => l.HouseholdId == household.HouseholdId && l.UserId == household.UserId, ct);
        if (link is not null) return link;
        link = GarminLink.Create(household.HouseholdId, household.UserId);
        db.GarminLinks.Add(link);
        return link;
    }
}
