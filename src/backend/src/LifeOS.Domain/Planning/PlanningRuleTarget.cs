using LifeOS.Domain.Library;

namespace LifeOS.Domain.Planning;

/// <summary>
/// What a <see cref="PlanningRule"/> pins to a given weekday/meal slot, mirroring the frontend's
/// <c>PlanningRuleTarget</c> discriminated union.
/// </summary>
public abstract record PlanningRuleTarget
{
    private PlanningRuleTarget()
    {
    }

    public sealed record Component(string ComponentId, ComponentType ComponentType) : PlanningRuleTarget;

    public sealed record Dish(string DishId) : PlanningRuleTarget;
}
