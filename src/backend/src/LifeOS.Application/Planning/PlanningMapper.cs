using LifeOS.Domain.Common;
using LifeOS.Domain.Library;
using LifeOS.Domain.Planning;

namespace LifeOS.Application.Planning;

/// <summary>
/// Maps <see cref="PlanningRule"/> and <see cref="FrequencyRule"/> aggregates to/from their API
/// read models and request payloads.
/// </summary>
internal static class PlanningMapper
{
    public static PlanningRuleDto ToDto(PlanningRule rule)
    {
        var target = rule.Target switch
        {
            PlanningRuleTarget.Component component => new PlanningRuleTargetDto(
                "component",
                component.ComponentId,
                component.ComponentType.ToString().ToLowerInvariant(),
                null),
            PlanningRuleTarget.Dish dish => new PlanningRuleTargetDto("dish", null, null, dish.DishId),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Target, "Unknown planning rule target."),
        };

        return new PlanningRuleDto(
            rule.Id,
            rule.Weekday.ToString().ToLowerInvariant(),
            rule.MealType.ToString().ToLowerInvariant(),
            target);
    }

    public static FrequencyRuleDto ToDto(FrequencyRule rule)
    {
        var target = rule.Target switch
        {
            FrequencyRuleTarget.Component component => new FrequencyRuleTargetDto("component", component.ComponentId, null, null, null),
            FrequencyRuleTarget.Dish dish => new FrequencyRuleTargetDto("dish", null, dish.DishId, null, null),
            FrequencyRuleTarget.Category category => new FrequencyRuleTargetDto("category", null, null, category.CategoryId, category.Label),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Target, "Unknown frequency rule target."),
        };

        return new FrequencyRuleDto(rule.Id, target, rule.TargetCountPerWeek);
    }

    public static PlanningRuleTarget ParsePlanningRuleTarget(PlanningRuleTargetDto target) => target.Kind switch
    {
        "component" when target.ComponentId is not null && target.ComponentType is not null
            => new PlanningRuleTarget.Component(target.ComponentId, ParseComponentType(target.ComponentType)),
        "dish" when target.DishId is not null => new PlanningRuleTarget.Dish(target.DishId),
        _ => throw new ArgumentException($"Invalid planning rule target for kind '{target.Kind}'.", nameof(target)),
    };

    public static FrequencyRuleTarget ParseFrequencyRuleTarget(FrequencyRuleTargetDto target) => target.Kind switch
    {
        "component" when target.ComponentId is not null => new FrequencyRuleTarget.Component(target.ComponentId),
        "dish" when target.DishId is not null => new FrequencyRuleTarget.Dish(target.DishId),
        "category" when target.CategoryId is not null && target.Label is not null
            => new FrequencyRuleTarget.Category(target.CategoryId, target.Label),
        _ => throw new ArgumentException($"Invalid frequency rule target for kind '{target.Kind}'.", nameof(target)),
    };

    public static Weekday ParseWeekday(string weekday) =>
        Enum.TryParse<Weekday>(weekday, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown weekday '{weekday}'.", nameof(weekday));

    public static MealType ParseMealType(string mealType) =>
        Enum.TryParse<MealType>(mealType, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown meal type '{mealType}'.", nameof(mealType));

    private static ComponentType ParseComponentType(string componentType) =>
        Enum.TryParse<ComponentType>(componentType, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown component type '{componentType}'.", nameof(componentType));
}
