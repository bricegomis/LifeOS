using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LifeOS.Api.Authentication;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
using LifeOS.Application.WeekPlanning;

namespace LifeOS.Api.Endpoints;

public static class WeekScenariosEndpoints
{
    public static IEndpointRouteBuilder MapWeekScenariosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/weeks/{weekId}/scenarios")
            .WithTags("WeekScenarios")
            .RequireAuthorization()
            .AddRequestValidation();

        group.MapGet("/", GetScenariosAsync)
            .WithName("GetScenarios")
            .Produces<List<StoredWeekScenarioDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/generate", GenerateScenariosAsync)
            .WithName("GenerateScenarios")
            .Produces<List<GeneratedWeekScenarioDto>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{scenarioId}/apply", ApplyScenarioAsync)
            .WithName("ApplyScenario")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// Retrieves all scenarios for a given week.
    /// GET /api/weeks/{weekId}/scenarios
    /// </summary>
    private static async Task<IResult> GetScenariosAsync(
        Guid weekId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IWeekScenarioRepository scenarioRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        // Verify week belongs to household (isolation)
        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            return Results.NotFound();
        }

        var scenarios = await scenarioRepository.GetAllForWeekAsync(weekId, cancellationToken);

        var dtos = scenarios.Select(s => new StoredWeekScenarioDto(
            s.Id,
            s.WeekId,
            s.RankingObjective,
            s.Explanation.RootElement.GetRawText(),
            s.Applied,
            s.CreatedAt,
            s.UpdatedAt)).ToList();

        return Results.Ok(dtos);
    }

    /// <summary>
    /// Generates scenarios for a week with specified ranking objectives.
    /// POST /api/weeks/{weekId}/scenarios/generate
    /// </summary>
    private static async Task<IResult> GenerateScenariosAsync(
        Guid weekId,
        ClaimsPrincipal user,
        GenerateScenariosRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IScenarioEngine engine,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        // Verify week belongs to household (isolation)
        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            return Results.NotFound();
        }

        if (request.Objectives == null || request.Objectives.Count == 0)
        {
            return Results.Problem(
                "At least one objective must be specified.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var scenarios = await engine.GenerateScenariosAsync(
                weekId,
                householdId,
                request.Objectives.AsReadOnly(),
                cancellationToken);

            var dtos = scenarios.Select(s => new GeneratedWeekScenarioDto(
                s.Scenario.Id,
                s.Scenario.WeekId,
                s.Scenario.RankingObjective,
                s.Explanation,
                s.Scenario.Applied,
                s.Scenario.CreatedAt,
                s.Scenario.UpdatedAt)).ToList();

            return Results.Created($"/api/weeks/{weekId}/scenarios", dtos);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Applies a scenario to a week.
    /// PATCH /api/weeks/{weekId}/scenarios/{scenarioId}/apply
    /// </summary>
    private static async Task<IResult> ApplyScenarioAsync(
        Guid weekId,
        Guid scenarioId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IScenarioEngine engine,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        // Verify week belongs to household (isolation)
        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            return Results.NotFound();
        }

        try
        {
            await engine.ApplyScenarioAsync(weekId, scenarioId, householdId, cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}

public record GenerateScenariosRequest(
    [property: Required, MinLength(1)] List<string> Objectives);

public sealed record StoredWeekScenarioDto(
    Guid Id,
    Guid WeekId,
    string RankingObjective,
    string Explanation,
    bool Applied,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record GeneratedWeekScenarioDto(
    Guid Id,
    Guid WeekId,
    string RankingObjective,
    ScenarioExplanation Explanation,
    bool Applied,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
