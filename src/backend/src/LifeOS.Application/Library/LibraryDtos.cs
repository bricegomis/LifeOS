namespace LifeOS.Application.Library;

/// <summary>
/// Read model returned by the API for a meal component, mirroring the frontend's
/// <c>MealComponent</c> shape.
/// </summary>
public sealed record MealComponentDto(
    string Id,
    string Name,
    string Icon,
    string ComponentType,
    double EstimatedCalories,
    double EstimatedProteinGrams,
    double EstimatedCarbohydrateGrams,
    double EstimatedFatGrams,
    double DefaultPortionQuantity,
    string Unit,
    bool Active);

/// <summary>
/// Read model returned by the API for a composite dish, mirroring the frontend's
/// <c>CompositeDish</c> shape.
/// </summary>
public sealed record CompositeDishDto(
    string Id,
    string Name,
    string Icon,
    double EstimatedCalories,
    double EstimatedProteinGrams,
    double EstimatedCarbohydrateGrams,
    double EstimatedFatGrams,
    int PreparationTimeMinutes,
    bool SuitableForBreakfast,
    bool SuitableForLunch,
    bool SuitableForDinner,
    bool? ChildFriendly,
    bool? SuitableForBatchCooking,
    bool Active);

/// <summary>
/// Read model returned by the API for an activity, mirroring the frontend's <c>Activity</c> shape.
/// </summary>
public sealed record ActivityDto(
    string Id,
    string Name,
    string Icon,
    int? DefaultDurationMinutes);
