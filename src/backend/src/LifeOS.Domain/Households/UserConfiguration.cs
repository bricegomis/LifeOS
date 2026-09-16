using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// User configuration for nutrition and activity targets within a household.
/// Stores the baseline daily energy expenditure and net deficit goal, along with
/// macro targets for deterministic nutrition calculations.
/// </summary>
public sealed class UserConfiguration : Entity
{
    public Guid HouseholdId { get; private set; }
    public decimal DailyBaseEnergyKcal { get; private set; } // baseline daily energy expenditure
    public decimal TargetNetDeficitKcal { get; private set; } // daily caloric deficit goal
    public decimal TargetProteinG { get; private set; } // daily protein target in grams
    public decimal TargetCarbsG { get; private set; } // daily carbs target in grams
    public decimal TargetFatsG { get; private set; } // daily fats target in grams
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserConfiguration(
        Guid id,
        Guid householdId,
        decimal dailyBaseEnergyKcal,
        decimal targetNetDeficitKcal,
        decimal targetProteinG,
        decimal targetCarbsG,
        decimal targetFatsG,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        HouseholdId = householdId;
        DailyBaseEnergyKcal = dailyBaseEnergyKcal;
        TargetNetDeficitKcal = targetNetDeficitKcal;
        TargetProteinG = targetProteinG;
        TargetCarbsG = targetCarbsG;
        TargetFatsG = targetFatsG;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static UserConfiguration Create(
        Guid householdId,
        decimal dailyBaseEnergyKcal,
        decimal targetNetDeficitKcal,
        decimal targetProteinG,
        decimal targetCarbsG,
        decimal targetFatsG,
        DateTimeOffset? now = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A user configuration must belong to a household.", nameof(householdId));
        }

        if (dailyBaseEnergyKcal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyBaseEnergyKcal), "Daily base energy cannot be negative.");
        }

        if (targetNetDeficitKcal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetNetDeficitKcal), "Target net deficit cannot be negative.");
        }

        if (targetProteinG < 0 || targetCarbsG < 0 || targetFatsG < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetProteinG), "Macro targets cannot be negative.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;

        return new UserConfiguration(
            Guid.NewGuid(),
            householdId,
            dailyBaseEnergyKcal,
            targetNetDeficitKcal,
            targetProteinG,
            targetCarbsG,
            targetFatsG,
            timestamp,
            timestamp);
    }

    /// <summary>
    /// Rehydrates a <see cref="UserConfiguration"/> from persisted state.
    /// </summary>
    public static UserConfiguration Rehydrate(
        Guid id,
        Guid householdId,
        decimal dailyBaseEnergyKcal,
        decimal targetNetDeficitKcal,
        decimal targetProteinG,
        decimal targetCarbsG,
        decimal targetFatsG,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new UserConfiguration(
            id,
            householdId,
            dailyBaseEnergyKcal,
            targetNetDeficitKcal,
            targetProteinG,
            targetCarbsG,
            targetFatsG,
            createdAt,
            updatedAt);
    }

    /// <summary>
    /// Updates user configuration.
    /// </summary>
    public void UpdateConfiguration(
        decimal? dailyBaseEnergyKcal = null,
        decimal? targetNetDeficitKcal = null,
        decimal? targetProteinG = null,
        decimal? targetCarbsG = null,
        decimal? targetFatsG = null,
        DateTimeOffset? now = null)
    {
        if (dailyBaseEnergyKcal.HasValue && dailyBaseEnergyKcal.Value >= 0)
        {
            DailyBaseEnergyKcal = dailyBaseEnergyKcal.Value;
        }

        if (targetNetDeficitKcal.HasValue && targetNetDeficitKcal.Value >= 0)
        {
            TargetNetDeficitKcal = targetNetDeficitKcal.Value;
        }

        if (targetProteinG.HasValue && targetProteinG.Value >= 0)
        {
            TargetProteinG = targetProteinG.Value;
        }

        if (targetCarbsG.HasValue && targetCarbsG.Value >= 0)
        {
            TargetCarbsG = targetCarbsG.Value;
        }

        if (targetFatsG.HasValue && targetFatsG.Value >= 0)
        {
            TargetFatsG = targetFatsG.Value;
        }

        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Computes the daily food target based on base energy, activity sessions, and deficit goal:
    /// daily_food_target = base_energy + activity_energy - deficit_goal
    /// </summary>
    public decimal ComputeDailyFoodTarget(decimal activityEnergyKcal)
    {
        return DailyBaseEnergyKcal + activityEnergyKcal - TargetNetDeficitKcal;
    }
}
