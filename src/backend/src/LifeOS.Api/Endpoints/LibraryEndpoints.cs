using LifeOS.Application.Library;

namespace LifeOS.Api.Endpoints;

public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/activities", async (GetActivitiesQuery activities, CancellationToken ct) =>
            Results.Ok(await activities.ExecuteAsync(ct))).RequireAuthorization().WithTags("Historical activities");
        return app;
    }
}
