namespace LifeOS.Domain.Library;

/// <summary>
/// A reusable, elementary meal building block (e.g. a protein or a starch), mirroring the
/// frontend's <c>MealComponent</c> model. Part of the shared, read-only meal library.
/// </summary>
public sealed record MealComponent(
    string Id,
    string Name,
    string Icon,
    ComponentType ComponentType,
    double EstimatedCalories,
    double EstimatedProteinGrams,
    double EstimatedCarbohydrateGrams,
    double EstimatedFatGrams,
    double DefaultPortionQuantity,
    string Unit,
    bool Active);
