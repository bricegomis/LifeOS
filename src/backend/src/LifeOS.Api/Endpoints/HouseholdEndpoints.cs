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
            .Produces<HouseholdDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/members", GetHouseholdMembersAsync)
            .WithName("GetHouseholdMembers")
            .Produces<HouseholdMembersResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", GetCurrentHouseholdAsync)
            .WithName("CreateOrGetCurrentHousehold")
            .Produces<HouseholdDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

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

        return Results.Ok(new HouseholdMembersResponse(
            household.Id,
            household.Members,
            household.MemberProfiles));
    }
}

public sealed record HouseholdMembersResponse(
    Guid Id,
    IReadOnlyList<HouseholdMemberDto> Members,
    IReadOnlyList<MemberProfileDto> MemberProfiles);
