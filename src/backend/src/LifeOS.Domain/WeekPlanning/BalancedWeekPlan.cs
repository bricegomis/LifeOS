using System.Text.Json;
using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// The balanced plan computed for a week.
/// <para>
/// There is a single planning method: the engine looks for the best compromise between
/// nutritional balance (treated as a priority constraint), budget, diversity over the month
/// and waste reduction. The user never picks a strategy; the previous per-objective
/// scenarios (nutritional_balance / economy / reduce_waste) no longer exist.
/// </para>
/// <para>
/// Persisted in the historical <c>week_scenarios</c> table: <see cref="Method"/> maps to the
/// legacy <c>RankingObjective</c> column and always holds <see cref="BalancedMethod"/> for
/// newly computed plans. Rows written by the previous engine keep their legacy value and are
/// still readable.
/// </para>
/// </summary>
public sealed class BalancedWeekPlan : Entity
{
    /// <summary>
    /// The only planning method produced by the engine.
    /// </summary>
    public const string BalancedMethod = "balanced";

    public Guid WeekId { get; private set; }

    /// <summary>
    /// Planning method used to compute the plan. Always <see cref="BalancedMethod"/> for plans
    /// computed by the current engine; legacy rows may carry an older per-objective value.
    /// </summary>
    public string Method { get; private set; }

    /// <summary>Structured breakdown: composite score, per-dimension scores, trade-offs and data limits.</summary>
    public JsonDocument Explanation { get; private set; }

    /// <summary>Whether the user retained this computed plan for the week.</summary>
    public bool Applied { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BalancedWeekPlan(
        Guid id,
        Guid weekId,
        string method,
        JsonDocument explanation,
        bool applied,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        WeekId = weekId;
        Method = method;
        Explanation = explanation;
        Applied = applied;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a newly computed balanced plan for a week.
    /// </summary>
    public static BalancedWeekPlan Create(
        Guid weekId,
        JsonDocument explanation,
        DateTimeOffset? now = null)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        ArgumentNullException.ThrowIfNull(explanation);

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new BalancedWeekPlan(
            Guid.NewGuid(),
            weekId,
            BalancedMethod,
            explanation,
            applied: false,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Marks the plan as the one retained for the week.
    /// </summary>
    public void Apply()
    {
        Applied = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reverts the plan from the retained state.
    /// </summary>
    public void Revert()
    {
        Applied = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rehydrates a <see cref="BalancedWeekPlan"/> from persisted state.
    /// </summary>
    public static BalancedWeekPlan Rehydrate(
        Guid id,
        Guid weekId,
        string method,
        JsonDocument explanation,
        bool applied,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new BalancedWeekPlan(id, weekId, method, explanation, applied, createdAt, updatedAt);
    }
}
