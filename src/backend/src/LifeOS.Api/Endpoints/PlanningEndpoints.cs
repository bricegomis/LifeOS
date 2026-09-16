using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
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
            .RequireAuthorization()
            .AddRequestValidation();

        planningRules.MapGet("/", GetPlanningRulesAsync)
            .WithName("GetPlanningRules")
            .Produces<List<PlanningRuleDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        planningRules.MapPost("/", CreatePlanningRuleAsync)
            .WithName("CreatePlanningRule")
            .Produces<PlanningRuleDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        planningRules.MapPut("/{ruleId:guid}", UpdatePlanningRuleAsync)
            .WithName("UpdatePlanningRule")
            .Produces<PlanningRuleDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        planningRules.MapDelete("/{ruleId:guid}", DeletePlanningRuleAsync)
            .WithName("DeletePlanningRule")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var frequencyRules = app.MapGroup("/api/frequency-rules")
            .WithTags("Planning")
            .RequireAuthorization()
            .AddRequestValidation();

        frequencyRules.MapGet("/", GetFrequencyRulesAsync)
            .WithName("GetFrequencyRules")
            .Produces<List<FrequencyRuleDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        frequencyRules.MapPost("/", CreateFrequencyRuleAsync)
            .WithName("CreateFrequencyRule")
            .Produces<FrequencyRuleDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        frequencyRules.MapPut("/{ruleId:guid}", UpdateFrequencyRuleAsync)
            .WithName("UpdateFrequencyRule")
            .Produces<FrequencyRuleDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        frequencyRules.MapDelete("/{ruleId:guid}", DeleteFrequencyRuleAsync)
            .WithName("DeleteFrequencyRule")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetPlanningRulesAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetPlanningRulesQuery getPlanningRulesQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        return Results.Ok(await getPlanningRulesQuery.ExecuteAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> CreatePlanningRuleAsync(
        ClaimsPrincipal user,
        PlanningRuleRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        CreatePlanningRuleCommand createPlanningRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var rule = await createPlanningRuleCommand.ExecuteAsync(
                householdId,
                request.Weekday,
                request.MealType,
                request.Target,
                cancellationToken);

            return Results.Created($"/api/planning-rules/{rule.Id}", rule);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdatePlanningRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        PlanningRuleRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        UpdatePlanningRuleCommand updatePlanningRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var rule = await updatePlanningRuleCommand.ExecuteAsync(
                householdId,
                ruleId,
                request.Weekday,
                request.MealType,
                request.Target,
                cancellationToken);

            return rule is null ? Results.NotFound() : Results.Ok(rule);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeletePlanningRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        DeletePlanningRuleCommand deletePlanningRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await deletePlanningRuleCommand.ExecuteAsync(householdId, ruleId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> GetFrequencyRulesAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetFrequencyRulesQuery getFrequencyRulesQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        return Results.Ok(await getFrequencyRulesQuery.ExecuteAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> CreateFrequencyRuleAsync(
        ClaimsPrincipal user,
        FrequencyRuleRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        CreateFrequencyRuleCommand createFrequencyRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var rule = await createFrequencyRuleCommand.ExecuteAsync(
                householdId,
                request.Target,
                request.TargetCountPerWeek,
                cancellationToken);

            return Results.Created($"/api/frequency-rules/{rule.Id}", rule);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdateFrequencyRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        FrequencyRuleTargetCountRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        UpdateFrequencyRuleCommand updateFrequencyRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var rule = await updateFrequencyRuleCommand.ExecuteAsync(householdId, ruleId, request.TargetCountPerWeek, cancellationToken);

        return rule is null ? Results.NotFound() : Results.Ok(rule);
    }

    private static async Task<IResult> DeleteFrequencyRuleAsync(
        ClaimsPrincipal user,
        Guid ruleId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        DeleteFrequencyRuleCommand deleteFrequencyRuleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await deleteFrequencyRuleCommand.ExecuteAsync(householdId, ruleId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
