using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;

namespace LifeOS.Api.Endpoints;

public static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/households")
            .WithTags("Households")
            .RequireAuthorization();

        group.MapGet("/current", GetCurrentHouseholdAsync)
            .WithName("GetCurrentHousehold")
            .WithOpenApi();

        group.MapGet("/members", GetHouseholdMembersAsync)
            .WithName("GetHouseholdMembers")
            .WithOpenApi();

        group.MapPost("/", GetCurrentHouseholdAsync)
            .WithName("CreateOrGetCurrentHousehold")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetCurrentHouseholdAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetCurrentHouseholdQuery getCurrentHouseholdQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var household = await getCurrentHouseholdQuery.ExecuteAsync(householdId, cancellationToken);

        return Results.Ok(household);
    }

    private static async Task<IResult> GetHouseholdMembersAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetCurrentHouseholdQuery getCurrentHouseholdQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var household = await getCurrentHouseholdQuery.ExecuteAsync(householdId, cancellationToken);

        return Results.Ok(new
        {
            household.Id,
            household.Members,
            household.MemberProfiles
        });
    }
}
