namespace LifeOS.Domain.Planning;

/// <summary>
/// What a <see cref="FrequencyRule"/> constrains, mirroring the frontend's
/// <c>FrequencyRuleTarget</c> discriminated union.
/// </summary>
public abstract record FrequencyRuleTarget
{
    private FrequencyRuleTarget()
    {
    }

    public sealed record Component(string ComponentId) : FrequencyRuleTarget;

    public sealed record Dish(string DishId) : FrequencyRuleTarget;

    public sealed record Category(string CategoryId, string Label) : FrequencyRuleTarget;
}
