using LifeOS.Domain.Common;

namespace LifeOS.Domain.Planning;

/// <summary>
/// Pins a meal component or dish to a specific weekday/meal slot, mirroring the frontend's
/// <c>PlanningRule</c> model. Aggregate root of the Planning bounded context.
/// </summary>
public sealed class PlanningRule
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Weekday Weekday { get; private set; }
    public MealType MealType { get; private set; }
    public PlanningRuleTarget Target { get; private set; }

    private PlanningRule(Guid id, Guid ownerId, Weekday weekday, MealType mealType, PlanningRuleTarget target)
    {
        Id = id;
        OwnerId = ownerId;
        Weekday = weekday;
        MealType = mealType;
        Target = target;
    }

    public static PlanningRule Create(Guid ownerId, Weekday weekday, MealType mealType, PlanningRuleTarget target)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("A planning rule must belong to an owner.", nameof(ownerId));
        }

        return new PlanningRule(Guid.NewGuid(), ownerId, weekday, mealType, target);
    }

    /// <summary>
    /// Rehydrates a <see cref="PlanningRule"/> from persisted state.
    /// </summary>
    public static PlanningRule Rehydrate(Guid id, Guid ownerId, Weekday weekday, MealType mealType, PlanningRuleTarget target)
    {
        return new PlanningRule(id, ownerId, weekday, mealType, target);
    }

    public void Update(Weekday weekday, MealType mealType, PlanningRuleTarget target)
    {
        Weekday = weekday;
        MealType = mealType;
        Target = target;
    }
}
