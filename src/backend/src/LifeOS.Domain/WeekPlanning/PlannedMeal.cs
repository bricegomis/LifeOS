using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

public sealed class PlannedMeal : Entity
{
    public Guid DayPlanId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public int? StartMinute { get; private set; }
    public int? EndMinute { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<PlannedRecipeLine> RecipeLines { get; private set; } = [];

    private PlannedMeal() { }

    private PlannedMeal(Guid id, Guid dayPlanId, Guid householdId, DateTimeOffset now) : base(id)
    {
        DayPlanId = dayPlanId;
        HouseholdId = householdId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static PlannedMeal CreateManual(Guid dayPlanId, Guid householdId)
    {
        if (dayPlanId == Guid.Empty || householdId == Guid.Empty)
            throw new ArgumentException("La journée et le foyer sont obligatoires.");
        return new PlannedMeal(Guid.NewGuid(), dayPlanId, householdId, DateTimeOffset.UtcNow);
    }

    public void Schedule(Guid dayPlanId, int? startMinute, int? endMinute)
    {
        if (dayPlanId == Guid.Empty) throw new ArgumentException("La journée est obligatoire.");
        EventTime.Validate(startMinute, endMinute);
        DayPlanId = dayPlanId;
        StartMinute = startMinute;
        EndMinute = endMinute;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
