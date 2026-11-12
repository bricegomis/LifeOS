using LifeOS.Application.Households;

namespace LifeOS.Api.Households;

public sealed record HouseholdMembersResponse(
    Guid Id,
    IReadOnlyList<HouseholdMemberDto> Members,
    IReadOnlyList<MemberProfileDto> MemberProfiles);

public static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/households").WithTags("Households").RequireAuthorization();
        group.MapGet("/current", GetCurrent).WithName("GetCurrentHousehold").Produces<HouseholdDto>();
        group.MapGet("/members", GetMembers).WithName("GetHouseholdMembers").Produces<HouseholdMembersResponse>();
        group.MapPost("/", GetCurrent).WithName("CreateOrGetCurrentHousehold").Produces<HouseholdDto>();
        return app;
    }

    private static async Task<IResult> GetCurrent(HouseholdContext household, GetCurrentHouseholdQuery query, CancellationToken ct) =>
        Results.Ok(await query.ExecuteAsync(household.HouseholdId, ct));

    private static async Task<IResult> GetMembers(HouseholdContext household, GetCurrentHouseholdQuery query, CancellationToken ct)
    {
        var current = await query.ExecuteAsync(household.HouseholdId, ct);
        return Results.Ok(new HouseholdMembersResponse(current.Id, current.Members, current.MemberProfiles));
    }
}
