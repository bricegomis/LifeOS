using System.Text.Json;
using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Application.WeekPlanning;

/// <summary>
/// Scenario explanation with calculated deltas and text explanation.
/// </summary>
public record ScenarioExplanation(
    string RankingObjective,
    decimal NutritionDeltaKcal,
    decimal BudgetDeltaEur,
    Dictionary<string, object> Details,
    string TextExplanation);

/// <summary>
/// Deterministic scenario generation engine for week planning.
/// Generates alternative meal suggestions based on ranking objectives.
/// </summary>
public interface IScenarioEngine
{
    /// <summary>
    /// Generates multiple deterministic scenarios for a week.
    /// </summary>
    /// <param name="weekId">The week to generate scenarios for</param>
    /// <param name="householdId">The household for isolation</param>
    /// <param name="objectives">The ranking objectives to generate scenarios for (nutritional_balance, economy, reduce_waste)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of generated scenarios with explanations</returns>
    Task<IReadOnlyList<(WeekScenario Scenario, ScenarioExplanation Explanation)>> GenerateScenariosAsync(
        Guid weekId,
        Guid householdId,
        IReadOnlyList<string> objectives,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a scenario to a week (replaces existing meals with scenario suggestions).
    /// </summary>
    /// <param name="weekId">The week to apply scenario to</param>
    /// <param name="scenarioId">The scenario to apply</param>
    /// <param name="householdId">The household for isolation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ApplyScenarioAsync(
        Guid weekId,
        Guid scenarioId,
        Guid householdId,
        CancellationToken cancellationToken = default);
}
