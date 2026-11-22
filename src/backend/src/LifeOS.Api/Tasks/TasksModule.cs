using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Tasks;

public static class TasksModule
{
    /// <summary>Management code shared by the Tasks endpoints and the assistant tools (ADR 0017).</summary>
    public static IServiceCollection AddTaskLibraries(this IServiceCollection services) => services
        .AddScoped<TaskProfiles>()
        .AddScoped<TodoLibrary>();

    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app) => app
        .MapTodosEndpoints();
}

/// <summary>
/// Who is responsible. Every todo belongs to a profile of the household core (ADR 0017); without a
/// profile, the household's main profile (its first one) is used.
/// </summary>
public sealed class TaskProfiles(LifeOSDbContext db)
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
