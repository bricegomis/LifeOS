using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Application.Planning;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the planning rules and frequency rules endpoints.
/// </summary>
public static class PlanningEndpoints
{
    public static IEndpointRouteBuilder MapPlanningEndpoints(this IEndpointRouteBuilder app)
    {
        var planningRules = app.MapGroup("/api/planning-rules")
            .WithTags("Planning")
            .RequireAuthorization();

        planningRules.MapGet("/", GetPlanningRulesAsync).WithName("GetPlanningRules");
        planningRules.MapPost("/", CreatePlanningRuleAsync).WithName("CreatePlanningRule");
        planningRules.MapPut("/{ruleId:guid}", UpdatePlanningRuleAsync).WithName("UpdatePlanningRule");
        planningRules.MapDelete("/{ruleId:guid}", DeletePlanningRuleAsync).WithName("DeletePlanningRule");

        var frequencyRules = app.MapGroup("/api/frequency-rules")
            .WithTags("Planning")
            .RequireAuthorization();

        frequencyRules.MapGet("/", GetFrequencyRulesAsync).WithName("GetFrequencyRules");
        frequencyRules.MapPost("/", CreateFrequencyRuleAsync).WithName("CreateFrequencyRule");
        frequencyRules.MapPut("/{ruleId:guid}", UpdateFrequencyRuleAsync).WithName("UpdateFrequencyRule");
        frequencyRules.MapDelete("/{ruleId:guid}", DeleteFrequencyRuleAsync).WithName("DeleteFrequencyRule");

        return app;
    }

    private static async Task<IResult> GetPlanningRulesAsync(
        ClaimsPrincipal user,
        GetPlanningRulesQuery getPlanningRulesQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await getPlanningRulesQuery.ExecuteAsync(ownerId, cancellationToken));
    }

    private static async Task<IResult> CreatePlanningRuleAsync(
        ClaimsPrincipal user,
        PlanningRuleRequest request,
        CreatePlanningRuleCommand createPlanningRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var rule = await createPlanningRuleCommand.ExecuteAsync(
                ownerId,
                request.Weekday,
                request.MealType,
                request.Target,
                cancellationToken);

            return Results.Created($"/api/planning-rules/{rule.Id}", rule);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdatePlanningRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        PlanningRuleRequest request,
        UpdatePlanningRuleCommand updatePlanningRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var rule = await updatePlanningRuleCommand.ExecuteAsync(
                ownerId,
                ruleId,
                request.Weekday,
                request.MealType,
                request.Target,
                cancellationToken);

            return rule is null ? Results.NotFound() : Results.Ok(rule);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> DeletePlanningRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        DeletePlanningRuleCommand deletePlanningRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        var deleted = await deletePlanningRuleCommand.ExecuteAsync(ownerId, ruleId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> GetFrequencyRulesAsync(
        ClaimsPrincipal user,
        GetFrequencyRulesQuery getFrequencyRulesQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await getFrequencyRulesQuery.ExecuteAsync(ownerId, cancellationToken));
    }

    private static async Task<IResult> CreateFrequencyRuleAsync(
        ClaimsPrincipal user,
        FrequencyRuleRequest request,
        CreateFrequencyRuleCommand createFrequencyRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var rule = await createFrequencyRuleCommand.ExecuteAsync(
                ownerId,
                request.Target,
                request.TargetCountPerWeek,
                cancellationToken);

            return Results.Created($"/api/frequency-rules/{rule.Id}", rule);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdateFrequencyRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        FrequencyRuleTargetCountRequest request,
        UpdateFrequencyRuleCommand updateFrequencyRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        var rule = await updateFrequencyRuleCommand.ExecuteAsync(ownerId, ruleId, request.TargetCountPerWeek, cancellationToken);

        return rule is null ? Results.NotFound() : Results.Ok(rule);
    }

    private static async Task<IResult> DeleteFrequencyRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        DeleteFrequencyRuleCommand deleteFrequencyRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        var deleted = await deleteFrequencyRuleCommand.ExecuteAsync(ownerId, ruleId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
