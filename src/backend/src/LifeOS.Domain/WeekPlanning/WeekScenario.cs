using System.Text.Json;
using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// A deterministic scenario suggestion for a week.
/// Contains alternative meals and explanations for applying the scenario.
/// </summary>
public sealed class WeekScenario : Entity
{
    public Guid WeekId { get; private set; }
    public string RankingObjective { get; private set; } // nutritional_balance, economy, reduce_waste
    public JsonDocument Explanation { get; private set; } // structured explanation with nutrition/budget deltas
    public bool Applied { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private WeekScenario(
        Guid id,
        Guid weekId,
        string rankingObjective,
        JsonDocument explanation,
        bool applied,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        WeekId = weekId;
        RankingObjective = rankingObjective;
        Explanation = explanation;
        Applied = applied;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Creates a new week scenario.
    /// </summary>
    public static WeekScenario Create(
        Guid weekId,
        string rankingObjective,
        JsonDocument explanation,
        DateTimeOffset? now = null)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        if (string.IsNullOrWhiteSpace(rankingObjective))
        {
            throw new ArgumentException("Ranking objective is required.", nameof(rankingObjective));
        }

        if (!IsValidObjective(rankingObjective))
        {
            throw new ArgumentException(
                $"Ranking objective must be one of: nutritional_balance, economy, reduce_waste. Got: {rankingObjective}",
                nameof(rankingObjective));
        }

        if (explanation == null)
        {
            throw new ArgumentNullException(nameof(explanation), "Explanation is required.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new WeekScenario(
            Guid.NewGuid(),
            weekId,
            rankingObjective.ToLowerInvariant(),
            explanation,
            applied: false,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Applies the scenario (marks it as applied).
    /// </summary>
    public void Apply()
    {
        Applied = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Revert the scenario from applied state.
    /// </summary>
    public void Revert()
    {
        Applied = false;
        UpdatedAt = DateTimeOffset.UtcNow;
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
    /// Rehydrates a <see cref="WeekScenario"/> from persisted state.
    /// </summary>
    public static WeekScenario Rehydrate(
        Guid id,
        Guid weekId,
        string rankingObjective,
        JsonDocument explanation,
        bool applied,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new WeekScenario(id, weekId, rankingObjective, explanation, applied, createdAt, updatedAt);
    }
}
