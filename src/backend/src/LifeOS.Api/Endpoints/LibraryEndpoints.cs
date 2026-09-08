using LifeOS.Application.Library;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the read-only, shared meal library endpoints (meal components, composite dishes,
/// activities). This library is not scoped per owner: it mirrors the frontend's static
/// <c>src/frontend/src/data/localLibrary.ts</c> content.
/// </summary>
public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/meal-components", GetMealComponentsAsync)
            .WithName("GetMealComponents")
            .WithTags("Library")
            .RequireAuthorization();

        app.MapGet("/api/composite-dishes", GetCompositeDishesAsync)
            .WithName("GetCompositeDishes")
            .WithTags("Library")
            .RequireAuthorization();

        app.MapGet("/api/activities", GetActivitiesAsync)
            .WithName("GetActivities")
            .WithTags("Library")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetMealComponentsAsync(
        GetMealComponentsQuery getMealComponentsQuery,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await getMealComponentsQuery.ExecuteAsync(cancellationToken));
    }

    private static async Task<IResult> GetCompositeDishesAsync(
        GetCompositeDishesQuery getCompositeDishesQuery,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await getCompositeDishesQuery.ExecuteAsync(cancellationToken));
    }

    private static async Task<IResult> GetActivitiesAsync(
        GetActivitiesQuery getActivitiesQuery,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await getActivitiesQuery.ExecuteAsync(cancellationToken));
    }
}
