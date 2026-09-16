namespace LifeOS.Application.Households;

/// <summary>
/// Read/write model for a <see cref="LifeOS.Domain.Households.UserConfiguration"/>.
/// </summary>
public sealed record UserConfigurationDto(
    Guid Id,
    Guid HouseholdId,
    decimal DailyBaseEnergyKcal,
    decimal TargetNetDeficitKcal,
    decimal TargetProteinG,
    decimal TargetCarbsG,
    decimal TargetFatsG,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Request model for creating or updating user configuration.
/// </summary>
public sealed record UpsertUserConfigurationRequest(
    decimal DailyBaseEnergyKcal,
    decimal TargetNetDeficitKcal,
    decimal TargetProteinG,
    decimal TargetCarbsG,
    decimal TargetFatsG);

/// <summary>
/// Daily nutrition target calculation result.
/// Formula: daily_food_target = base_energy + activity_energy - deficit_goal
/// </summary>
public sealed record DailyNutritionTargetDto(
    Guid DayPlanId,
    decimal DailyBaseEnergyKcal,
    decimal ActivityEnergyKcal,
    decimal TargetNetDeficitKcal,
    decimal DailyFoodTargetKcal,
    decimal TargetProteinG,
    decimal TargetCarbsG,
    decimal TargetFatsG);
