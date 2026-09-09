using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Library;

/// <summary>
/// Use case: list every meal component in the shared library.
/// </summary>
public sealed class GetMealComponentsQuery(IMealComponentRepository mealComponentRepository)
{
    private readonly IMealComponentRepository _mealComponentRepository = mealComponentRepository;

    public async Task<IReadOnlyList<MealComponentDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var components = await _mealComponentRepository.GetAllAsync(cancellationToken);

        return components
            .Select(component => new MealComponentDto(
                component.Id,
                component.Name,
                component.Icon,
                component.ComponentType.ToString().ToLowerInvariant(),
                component.EstimatedCalories,
                component.EstimatedProteinGrams,
                component.EstimatedCarbohydrateGrams,
                component.EstimatedFatGrams,
                component.DefaultPortionQuantity,
                component.Unit,
                component.Active))
            .ToList();
    }
}

/// <summary>
/// Use case: list every composite dish in the shared library.
/// </summary>
public sealed class GetCompositeDishesQuery(ICompositeDishRepository compositeDishRepository)
{
    private readonly ICompositeDishRepository _compositeDishRepository = compositeDishRepository;

    public async Task<IReadOnlyList<CompositeDishDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var dishes = await _compositeDishRepository.GetAllAsync(cancellationToken);

        return dishes
            .Select(dish => new CompositeDishDto(
                dish.Id,
                dish.Name,
                dish.Icon,
                dish.EstimatedCalories,
                dish.EstimatedProteinGrams,
                dish.EstimatedCarbohydrateGrams,
                dish.EstimatedFatGrams,
                dish.PreparationTimeMinutes,
                dish.SuitableForBreakfast,
                dish.SuitableForLunch,
                dish.SuitableForDinner,
                dish.ChildFriendly,
                dish.SuitableForBatchCooking,
                dish.Active))
            .ToList();
    }
}

/// <summary>
/// Use case: list every activity in the shared library.
/// </summary>
public sealed class GetActivitiesQuery(IActivityRepository activityRepository)
{
    private readonly IActivityRepository _activityRepository = activityRepository;

    public async Task<IReadOnlyList<ActivityDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var activities = await _activityRepository.GetAllAsync(cancellationToken);

        return activities
            .Select(activity => new ActivityDto(activity.Id, activity.Name, activity.Icon, activity.DefaultDurationMinutes))
            .ToList();
    }
}
