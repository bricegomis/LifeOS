using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Sports;

public static class SportsModule
{
    /// <summary>Management code shared by the Sport endpoints and the assistant tools (ADR 0011, ADR 0013).</summary>
    public static IServiceCollection AddSportLibraries(this IServiceCollection services) => services
        .AddScoped<SportProfiles>()
        .AddScoped<SportTemplateLibrary>()
        .AddScoped<SportSessionLibrary>()
        .AddScoped<WeeklyGoalLibrary>()
        .AddScoped<DailyChallengeLibrary>();

    public static IEndpointRouteBuilder MapSportEndpoints(this IEndpointRouteBuilder app) => app
        .MapSportTemplatesEndpoints()
        .MapSportSessionsEndpoints()
        .MapWeeklyGoalsEndpoints()
        .MapDailyChallengesEndpoints()
        .MapSportOverviewEndpoints();
}

/// <summary>
/// Whose sport it is. Every sport row belongs to a profile of the household core (ADR 0013); without
/// a profile, the household's main profile (its first one) is used.
/// </summary>
public sealed class SportProfiles(LifeOSDbContext db)
{
    public async Task<Guid> ResolveAsync(Guid householdId, Guid? profileId, CancellationToken ct)
    {
        if (profileId is { } id)
        {
            return await db.MemberProfiles.AnyAsync(p => p.Id == id && p.HouseholdId == householdId, ct)
                ? id
                : throw new ArgumentException("Profil introuvable dans votre foyer.");
        }
        return await db.MemberProfiles.Where(p => p.HouseholdId == householdId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("Aucun profil dans votre foyer.");
    }
}
