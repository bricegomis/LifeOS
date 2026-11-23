using LifeOS.Api.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Health;

public static class HealthModule
{
    /// <summary>Management code shared by the Health endpoints and the assistant tools (ADR 0018).</summary>
    public static IServiceCollection AddHealthLibraries(this IServiceCollection services) => services
        .AddScoped<HealthOwners>()
        .AddScoped<SupplementLibrary>()
        .AddScoped<SymptomLibrary>()
        .AddScoped<AnalysisLibrary>()
        .AddScoped<HealthDocumentLibrary>()
        .AddScoped<AppointmentLibrary>()
        .AddScoped<WellnessLibrary>();

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app) => app
        .MapSupplementsEndpoints()
        .MapSymptomsEndpoints()
        .MapAnalysesEndpoints()
        .MapDocumentsEndpoints()
        .MapWellnessEndpoints();
}

/// <summary>
/// Whose health it is (ADR 0018): the caller's account, in its household, on the household's main profile.
/// Every health query filters on the household and on the account, so no other member sees it.
/// </summary>
public sealed record HealthOwner(Guid HouseholdId, Guid UserId, Guid ProfileId);

public sealed class HealthOwners(LifeOSDbContext db)
{
    public async Task<HealthOwner> ResolveAsync(HouseholdContext household, CancellationToken ct)
    {
        var profile = await db.MemberProfiles.Where(p => p.HouseholdId == household.HouseholdId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("Aucun profil dans votre foyer.");
        return new HealthOwner(household.HouseholdId, household.UserId, profile);
    }
}
