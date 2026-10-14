using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekPlanning;

/// <summary>
/// A planned week, conserved after its passage, duplicable.
/// Aggregate root of the WeekPlanning bounded context.
/// </summary>
public sealed class Week : Entity
{
    public Guid HouseholdId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public bool IsManual { get; private set; }
    public string TimeZoneId { get; private set; } = "Europe/Paris";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation property for EF Core
    public ICollection<DayPlan> DayPlans { get; set; } = [];

    private Week() { }
    public static Week CreateManual(Guid householdId, DateOnly startsOn, string timeZoneId)
    {
        if (startsOn.DayOfWeek != DayOfWeek.Monday) throw new ArgumentException("La semaine commence un lundi.");
        if (timeZoneId.Length > 100 || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
            throw new ArgumentException("Fuseau horaire inconnu.");
        if (householdId == Guid.Empty) throw new ArgumentException("Le foyer est obligatoire.");
        var week = new Week(Guid.NewGuid(), householdId, startsOn, DateTimeOffset.UtcNow);
        week.TimeZoneId = timeZoneId;
        for (var i = 0; i < 7; i++) week.DayPlans.Add(DayPlan.Create(week.Id, startsOn.AddDays(i), "home", false));
        return week;
    }

    private Week(Guid id, Guid householdId, DateOnly startsOn, DateTimeOffset now)
        : base(id)
    {
        HouseholdId = householdId;
        StartsOn = startsOn;
        IsManual = true;
        CreatedAt = now;
        UpdatedAt = now;
    }
}
