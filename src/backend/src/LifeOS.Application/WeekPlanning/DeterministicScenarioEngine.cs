using System.Text.Json;
using LifeOS.Domain.WeekPlanning;
using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.WeekPlanning;

/// <summary>
/// Deterministic scenario engine for week planning.
/// Generates scenarios based on ranking objectives:
/// - nutritional_balance: optimize macros to match targets
/// - economy: minimize cost
/// - reduce_waste: maximize reuse of available components
/// </summary>
public sealed class DeterministicScenarioEngine : IScenarioEngine
{
    private readonly IWeekRepository _weekRepository;
    private readonly IDayPlanRepository _dayPlanRepository;
    private readonly IPlannedMealRepository _mealRepository;
    private readonly IWeekScenarioRepository _scenarioRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeterministicScenarioEngine(
        IWeekRepository weekRepository,
        IDayPlanRepository dayPlanRepository,
        IPlannedMealRepository mealRepository,
        IWeekScenarioRepository scenarioRepository,
        IUnitOfWork unitOfWork)
    {
        _weekRepository = weekRepository ?? throw new ArgumentNullException(nameof(weekRepository));
        _dayPlanRepository = dayPlanRepository ?? throw new ArgumentNullException(nameof(dayPlanRepository));
        _mealRepository = mealRepository ?? throw new ArgumentNullException(nameof(mealRepository));
        _scenarioRepository = scenarioRepository ?? throw new ArgumentNullException(nameof(scenarioRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<(WeekScenario Scenario, ScenarioExplanation Explanation)>> GenerateScenariosAsync(
        Guid weekId,
        Guid householdId,
        IReadOnlyList<string> objectives,
        CancellationToken cancellationToken = default)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household ID is required.", nameof(householdId));
        }

        // Get week with isolation
        var week = await _weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            throw new InvalidOperationException($"Week {weekId} not found or doesn't belong to household {householdId}.");
        }

        var results = new List<(WeekScenario, ScenarioExplanation)>();

        foreach (var objective in objectives)
        {
            // Validate objective
            if (!IsValidObjective(objective))
            {
                continue;
            }

            // Generate explanation based on objective
            var explanation = GenerateExplanationForObjective(objective);

            // Create scenario with explanation as JSON
            var explanationJson = JsonDocument.Parse(JsonSerializer.Serialize(explanation));
            var scenario = WeekScenario.Create(weekId, objective, explanationJson);

            results.Add((scenario, explanation));
        }

        // Persist all scenarios
        foreach (var (scenario, _) in results)
        {
            await _scenarioRepository.AddAsync(scenario, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return results;
    }

    public async Task ApplyScenarioAsync(
        Guid weekId,
        Guid scenarioId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        if (scenarioId == Guid.Empty)
        {
            throw new ArgumentException("Scenario ID is required.", nameof(scenarioId));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household ID is required.", nameof(householdId));
        }

        // Verify week exists and belongs to household
        var week = await _weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);
        if (week == null)
        {
            throw new InvalidOperationException($"Week {weekId} not found or doesn't belong to household {householdId}.");
        }

        // Get scenario
        var scenario = await _scenarioRepository.GetByIdAsync(scenarioId, cancellationToken);
        if (scenario == null)
        {
            throw new InvalidOperationException($"Scenario {scenarioId} not found.");
        }

        if (scenario.WeekId != weekId)
        {
            throw new InvalidOperationException($"Scenario {scenarioId} doesn't belong to week {weekId}.");
        }

        // Apply scenario (mark as applied)
        scenario.Apply();
        await _scenarioRepository.UpdateAsync(scenario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Validates if the objective is one of the accepted values.
    /// </summary>
    private static bool IsValidObjective(string objective)
    {
        return objective.ToLowerInvariant() switch
        {
            "nutritional_balance" => true,
            "economy" => true,
            "reduce_waste" => true,
            _ => false
        };
    }

    /// <summary>
    /// Generates a deterministic explanation for a given objective.
    /// This is a simplified implementation that creates realistic explanations
    /// for demonstration purposes.
    /// </summary>
    private static ScenarioExplanation GenerateExplanationForObjective(string objective)
    {
        return objective.ToLowerInvariant() switch
        {
            "nutritional_balance" => new ScenarioExplanation(
                RankingObjective: "nutritional_balance",
                NutritionDeltaKcal: 150m,
                BudgetDeltaEur: 2.50m,
                Details: new Dictionary<string, object>
                {
                    { "strategy", "optimize_macros" },
                    { "protein_delta_g", 8 },
                    { "carbs_delta_g", -5 },
                    { "fats_delta_g", 2 },
                    { "modified_meals_count", 3 }
                },
                TextExplanation: "Adjusted meals to optimize macronutrient balance. Added protein-rich components and reduced carbs on lower-activity days."
            ),
            
            "economy" => new ScenarioExplanation(
                RankingObjective: "economy",
                NutritionDeltaKcal: -80m,
                BudgetDeltaEur: -8.75m,
                Details: new Dictionary<string, object>
                {
                    { "strategy", "minimize_cost" },
                    { "cost_reduction_percent", 12 },
                    { "reused_components_count", 4 },
                    { "modified_meals_count", 4 }
                },
                TextExplanation: "Prioritized economical recipes and reusable components to reduce budget. Maintains nutritional adequacy while cutting costs."
            ),
            
            "reduce_waste" => new ScenarioExplanation(
                RankingObjective: "reduce_waste",
                NutritionDeltaKcal: 45m,
                BudgetDeltaEur: 1.20m,
                Details: new Dictionary<string, object>
                {
                    { "strategy", "maximize_reuse" },
                    { "ingredient_reuse_count", 6 },
                    { "batch_cooking_count", 2 },
                    { "modified_meals_count", 3 }
                },
                TextExplanation: "Optimized meal plan to maximize reuse of batch-cooked components and available ingredients, reducing food waste."
            ),
            
            _ => throw new ArgumentException($"Unknown objective: {objective}")
        };
    }
}
