using System.Security.Claims;
using System.Text.Json;
using LifeOS.Api.Authentication;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
using LifeOS.Application.WeekPlanning;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Endpoints for the balanced plan of a week.
/// <para>
/// The caller no longer picks a ranking objective: computing a plan takes no body, and the
/// engine always looks for the same compromise between nutritional balance, budget, diversity
/// over the month and waste reduction.
/// </para>
/// </summary>
public static class WeekBalancedPlanEndpoints
{
    public static IEndpointRouteBuilder MapWeekBalancedPlanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/weeks/{weekId}/balanced-plan")
            .WithTags("WeekBalancedPlan")
            .RequireAuthorization()
            .AddRequestValidation();

        group.MapGet("/", GetBalancedPlansAsync)
            .WithName("GetBalancedPlans")
            .Produces<List<StoredBalancedPlanDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/compute", ComputeBalancedPlanAsync)
            .WithName("ComputeBalancedPlan")
            .Produces<ComputedBalancedPlanDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{planId}/apply", ApplyBalancedPlanAsync)
            .WithName("ApplyBalancedPlan")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// Lists the balanced plans computed for a week, most recent first.
    /// GET /api/weeks/{weekId}/balanced-plan
    /// </summary>
    private static async Task<IResult> GetBalancedPlansAsync(
        Guid weekId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IBalancedWeekPlanRepository planRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            return Results.NotFound();
        }

        var plans = await planRepository.GetAllForWeekAsync(weekId, cancellationToken);

        var dtos = plans
            .OrderByDescending(plan => plan.CreatedAt)
            .Select(plan => new StoredBalancedPlanDto(
                plan.Id,
                plan.WeekId,
                plan.Method,
                plan.Explanation.RootElement.GetRawText(),
                plan.Applied,
                plan.CreatedAt,
                plan.UpdatedAt))
            .ToList();

        return Results.Ok(dtos);
    }

    /// <summary>
    /// Computes the balanced plan of a week. Takes no parameter: there is nothing to choose.
    /// POST /api/weeks/{weekId}/balanced-plan/compute
    /// </summary>
    private static async Task<IResult> ComputeBalancedPlanAsync(
        Guid weekId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IBalancedPlanEngine engine,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            return Results.NotFound();
        }

        if (week.IsManual) return Results.Problem("Une semaine manuelle ne peut pas être recalculée automatiquement.", statusCode: 400);
        try
        {
            var (plan, explanation) = await engine.ComputeAsync(weekId, householdId, cancellationToken);

            var dto = new ComputedBalancedPlanDto(
                plan.Id,
                plan.WeekId,
                plan.Method,
                explanation,
                plan.Applied,
                plan.CreatedAt,
                plan.UpdatedAt);

            return Results.Created($"/api/weeks/{weekId}/balanced-plan", dto);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Retains a computed plan for the week.
    /// PATCH /api/weeks/{weekId}/balanced-plan/{planId}/apply
    /// </summary>
    private static async Task<IResult> ApplyBalancedPlanAsync(
        Guid weekId,
        Guid planId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IBalancedPlanEngine engine,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            return Results.NotFound();
        }

        if (week.IsManual) return Results.Problem("Une semaine manuelle ne peut pas recevoir un menu automatique.", statusCode: 400);
        try
        {
            await engine.ApplyAsync(weekId, planId, householdId, cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}

/// <summary>A persisted plan, whose explanation is returned as raw JSON.</summary>
public sealed record StoredBalancedPlanDto(
    Guid Id,
    Guid WeekId,
    string Method,
    string Explanation,
    bool Applied,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A freshly computed plan, whose explanation is returned structured.</summary>
public sealed record ComputedBalancedPlanDto(
    Guid Id,
    Guid WeekId,
    string Method,
    BalancedPlanExplanation Explanation,
    bool Applied,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
