namespace LifeOS.Domain.Planning;

/// <summary>
/// Constrains how many times per week a component, dish or category should appear, mirroring the
/// frontend's <c>FrequencyRule</c> model. Aggregate root of the Planning bounded context.
/// </summary>
public sealed class FrequencyRule
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public FrequencyRuleTarget Target { get; private set; }
    public int TargetCountPerWeek { get; private set; }

    private FrequencyRule(Guid id, Guid ownerId, FrequencyRuleTarget target, int targetCountPerWeek)
    {
        Id = id;
        OwnerId = ownerId;
        Target = target;
        TargetCountPerWeek = targetCountPerWeek;
    }

    public static FrequencyRule Create(Guid ownerId, FrequencyRuleTarget target, int targetCountPerWeek)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("A frequency rule must belong to an owner.", nameof(ownerId));
        }

        return new FrequencyRule(Guid.NewGuid(), ownerId, target, NormalizeCount(targetCountPerWeek));
    }

    /// <summary>
    /// Rehydrates a <see cref="FrequencyRule"/> from persisted state.
    /// </summary>
    public static FrequencyRule Rehydrate(Guid id, Guid ownerId, FrequencyRuleTarget target, int targetCountPerWeek)
    {
        return new FrequencyRule(id, ownerId, target, targetCountPerWeek);
    }

    public void UpdateTargetCount(int targetCountPerWeek)
    {
        TargetCountPerWeek = NormalizeCount(targetCountPerWeek);
    }

    private static int NormalizeCount(int value) => Math.Max(0, value);
}
