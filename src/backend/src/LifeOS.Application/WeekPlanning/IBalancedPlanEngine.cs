using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Application.WeekPlanning;

/// <summary>
/// Score of one dimension of the compromise. <see cref="Score"/> is <c>null</c> when the
/// household data does not allow computing it; the dimension is then excluded from the
/// composite score and reported in <see cref="BalancedPlanExplanation.Limitations"/>.
/// </summary>
/// <param name="Key">Stable identifier: nutrition, cost, diversity or waste.</param>
/// <param name="Score">Normalised score between 0 and 1, or <c>null</c> when not computable.</param>
/// <param name="Weight">Effective weight of the dimension in the composite score (0 when excluded).</param>
/// <param name="DataCoverage">Share of the week backed by real data for this dimension (0 to 1).</param>
/// <param name="Summary">Short human readable summary, in French, shown in the UI.</param>
/// <param name="Metrics">Raw measures behind the score.</param>
public sealed record BalancedPlanDimension(
    string Key,
    double? Score,
    double Weight,
    double DataCoverage,
    string Summary,
    IReadOnlyDictionary<string, double> Metrics);

/// <summary>
/// Structured result of the balanced computation, persisted as the plan explanation.
/// </summary>
/// <param name="Method">Always <see cref="BalancedWeekPlan.BalancedMethod"/>.</param>
/// <param name="OverallScore">Composite score between 0 and 1, or <c>null</c> when nothing is computable.</param>
/// <param name="NutritionConstraintMet">
/// Whether nutritional balance stays within the accepted band. Nutrition is a constraint:
/// when it is not met, the composite score is capped by the nutrition score so a cheap but
/// unbalanced week can never look good.
/// </param>
/// <param name="Dimensions">Per-dimension scores, always in the order nutrition, cost, diversity, waste.</param>
/// <param name="Tradeoffs">Explicit arbitrations the engine made, in French.</param>
/// <param name="Limitations">Data gaps that make part of the result approximate, in French.</param>
/// <param name="TextExplanation">One paragraph summary, in French.</param>
public sealed record BalancedPlanExplanation(
    string Method,
    double? OverallScore,
    bool NutritionConstraintMet,
    IReadOnlyList<BalancedPlanDimension> Dimensions,
    IReadOnlyList<string> Tradeoffs,
    IReadOnlyList<string> Limitations,
    string TextExplanation);

/// <summary>
/// Computes the single balanced plan of a week.
/// <para>
/// The user never chooses a strategy. The engine always looks for the same compromise:
/// nutritional balance first (as a constraint), then the lowest reachable budget, the least
/// waste and enough diversity over the month. Every arbitration and every data gap is reported
/// so the result stays honest about what it can and cannot guarantee.
/// </para>
/// </summary>
public interface IBalancedPlanEngine
{
    /// <summary>
    /// Computes and persists the balanced plan of a week.
    /// </summary>
    Task<(BalancedWeekPlan Plan, BalancedPlanExplanation Explanation)> ComputeAsync(
        Guid weekId,
        Guid householdId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retains a previously computed plan for the week.
    /// </summary>
    Task ApplyAsync(
        Guid weekId,
        Guid planId,
        Guid householdId,
        CancellationToken cancellationToken = default);
}
