using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.WeekContexts;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the current user's week planning context endpoints (get / replace).
/// </summary>
public static class WeekContextEndpoints
{
    public static IEndpointRouteBuilder MapWeekContextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/week-context")
            .WithTags("WeekContext")
            .RequireAuthorization();

        group.MapGet("/", GetWeekContextAsync)
            .WithName("GetWeekContext");

        group.MapPut("/", SaveWeekContextAsync)
            .WithName("SaveWeekContext");

        return app;
    }

    private static async Task<IResult> GetWeekContextAsync(
        ClaimsPrincipal user,
        GetWeekContextQuery getWeekContextQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await getWeekContextQuery.ExecuteAsync(ownerId, cancellationToken));
    }

    private static async Task<IResult> SaveWeekContextAsync(
        ClaimsPrincipal user,
        WeekContextDto request,
        SaveWeekContextCommand saveWeekContextCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        try
        {
            return Results.Ok(await saveWeekContextCommand.ExecuteAsync(ownerId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (FormatException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
