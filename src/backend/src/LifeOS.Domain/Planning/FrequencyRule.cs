using LifeOS.Domain.WeekContexts;

namespace LifeOS.Domain.Planning;

/// <summary>
/// Constrains how many times per week a component, dish or category should appear, mirroring the
/// frontend's <c>FrequencyRule</c> model. Aggregate root of the Planning bounded context.
/// </summary>
public sealed class FrequencyRule
{
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public FrequencyRuleTarget Target { get; private set; } = null!;
    public int TargetCountPerWeek { get; private set; }
    public WeekMode? WeekMode { get; private set; }

    private FrequencyRule()
    {
    }

    private FrequencyRule(
        Guid id,
        Guid householdId,
        FrequencyRuleTarget target,
        int targetCountPerWeek,
        WeekMode? weekMode)
    {
        Id = id;
        HouseholdId = householdId;
        Target = target;
        TargetCountPerWeek = targetCountPerWeek;
        WeekMode = weekMode;
    }

    public static FrequencyRule Create(
        Guid householdId,
        FrequencyRuleTarget target,
        int targetCountPerWeek,
        WeekMode? weekMode = null)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A frequency rule must belong to a household.", nameof(householdId));
        }

        return new FrequencyRule(Guid.NewGuid(), householdId, target, NormalizeCount(targetCountPerWeek), weekMode);
    }

    /// <summary>
    /// Rehydrates a <see cref="FrequencyRule"/> from persisted state.
    /// </summary>
    public static FrequencyRule Rehydrate(
        Guid id,
        Guid householdId,
        FrequencyRuleTarget target,
        int targetCountPerWeek,
        WeekMode? weekMode = null)
    {
        return new FrequencyRule(id, householdId, target, targetCountPerWeek, weekMode);
    }

    public void UpdateTargetCount(int targetCountPerWeek, WeekMode? weekMode = null)
    {
        TargetCountPerWeek = NormalizeCount(targetCountPerWeek);
        WeekMode = weekMode;
    }

    private static int NormalizeCount(int value) => Math.Max(0, value);
}
