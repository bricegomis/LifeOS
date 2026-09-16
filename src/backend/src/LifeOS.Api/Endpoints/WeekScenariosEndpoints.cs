using System.Security.Claims;
using System.Text.Json;
using LifeOS.Api.Authentication;
using LifeOS.Application.Households;
using LifeOS.Application.WeekPlanning;

namespace LifeOS.Api.Endpoints;

public static class WeekScenariosEndpoints
{
    public static IEndpointRouteBuilder MapWeekScenariosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/weeks/{weekId}/scenarios")
            .WithTags("WeekScenarios")
            .RequireAuthorization();

        group.MapGet("/", GetScenariosAsync)
            .WithName("GetScenarios")
            .WithOpenApi();

        group.MapPost("/generate", GenerateScenariosAsync)
            .WithName("GenerateScenarios")
            .WithOpenApi();

        group.MapPatch("/{scenarioId}/apply", ApplyScenarioAsync)
            .WithName("ApplyScenario")
            .WithOpenApi();

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

        var dtos = scenarios.Select(s => new
        {
            s.Id,
            s.WeekId,
            s.RankingObjective,
            Explanation = s.Explanation.RootElement.GetRawText(),
            s.Applied,
            s.CreatedAt,
            s.UpdatedAt
        }).ToList();

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
            return Results.BadRequest(new { error = "At least one objective must be specified." });
        }

        try
        {
            var scenarios = await engine.GenerateScenariosAsync(
                weekId,
                householdId,
                request.Objectives.AsReadOnly(),
                cancellationToken);

            var dtos = scenarios.Select(s => new
            {
                s.Scenario.Id,
                s.Scenario.WeekId,
                s.Scenario.RankingObjective,
                Explanation = new
                {
                    s.Explanation.RankingObjective,
                    s.Explanation.NutritionDeltaKcal,
                    s.Explanation.BudgetDeltaEur,
                    s.Explanation.Details,
                    s.Explanation.TextExplanation
                },
                s.Scenario.Applied,
                s.Scenario.CreatedAt,
                s.Scenario.UpdatedAt
            }).ToList();

            return Results.Created($"/api/weeks/{weekId}/scenarios", dtos);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
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
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

public record GenerateScenariosRequest(List<string> Objectives);
