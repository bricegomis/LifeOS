using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;
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
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetWeekContextQuery getWeekContextQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        return Results.Ok(await getWeekContextQuery.ExecuteAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> SaveWeekContextAsync(
        ClaimsPrincipal user,
        WeekContextDto request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        SaveWeekContextCommand saveWeekContextCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            return Results.Ok(await saveWeekContextCommand.ExecuteAsync(householdId, request, cancellationToken));
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
