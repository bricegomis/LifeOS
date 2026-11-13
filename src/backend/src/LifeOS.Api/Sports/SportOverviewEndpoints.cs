using LifeOS.Api.Households;
using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;

namespace LifeOS.Api.Sports;

public sealed record SportOption(string Code, string Label, bool HasDistance);

public sealed record SportCatalogDto(IReadOnlyList<SportOption> Sports, DateOnly Today);

/// <summary>Read-only views of the Sport module, all through its published contract.</summary>
public static class SportOverviewEndpoints
{
    public static SportCatalogDto Catalog(DateOnly today) => new(
        SportCatalog.Sports.Select(s => new SportOption(s.Key, s.Value, SportCatalog.HasDistance(s.Key))).ToList(), today);

    public static IEndpointRouteBuilder MapSportOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sport").RequireAuthorization().WithTags("Sport");
        g.MapGet("/catalog", (SportReadContract contract) => Results.Ok(Catalog(contract.Today()))).Produces<SportCatalogDto>();
        g.MapGet("/weeks", async (Guid? profileId, int? count, DateOnly? until, HouseholdContext household, SportProfiles profiles,
                SportReadContract contract, CancellationToken ct) =>
            Results.Ok(await contract.WeeksAsync(household.HouseholdId, await profiles.ResolveAsync(household.HouseholdId, profileId, ct),
                until ?? contract.Today(), count ?? 1, ct)))
            .Produces<List<WeekReportDto>>();
        g.MapGet("/days", async (DateOnly from, DateOnly to, HouseholdContext household, SportReadContract contract, CancellationToken ct) =>
            Results.Ok(await contract.DaysAsync(household.HouseholdId, from, to, ct))).Produces<List<SportDayDto>>();
        return app;
    }
}
