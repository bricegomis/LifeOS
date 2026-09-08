namespace LifeOS.Domain.Library;

/// <summary>
/// A complete dish treated as a single unit, mirroring the frontend's <c>CompositeDish</c> model.
/// Part of the shared, read-only meal library.
/// </summary>
public sealed record CompositeDish(
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
