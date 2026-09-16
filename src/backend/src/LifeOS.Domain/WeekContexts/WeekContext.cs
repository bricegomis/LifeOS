using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekContexts;

/// <summary>
/// A household's week planning context (alternating week configuration, per-week overrides and
/// per-day work/commute settings), mirroring the frontend's <c>WeekContext</c> model. Aggregate
/// root of the WeekContexts bounded context; there is exactly one per household.
/// </summary>
public sealed class WeekContext
{
    private Dictionary<Weekday, DayContext> _days = [];
    private List<WeekModeOverride> _weekModeOverrides = [];

    public Guid HouseholdId { get; private set; }
    public AlternatingWeekConfig AlternatingWeekConfig { get; private set; } = null!;
    public IReadOnlyList<WeekModeOverride> WeekModeOverrides => _weekModeOverrides;
    public IReadOnlyDictionary<Weekday, DayContext> Days => _days;

    private WeekContext()
    {
    }

    private WeekContext(
        Guid householdId,
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days)
    {
        HouseholdId = householdId;
        AlternatingWeekConfig = alternatingWeekConfig;
        _weekModeOverrides = [.. weekModeOverrides];
        _days = new Dictionary<Weekday, DayContext>(days);
    }

    public static WeekContext CreateDefault(Guid householdId, DateOnly referenceWeekStartDate)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A week context must belong to a household.", nameof(householdId));
        }

        var defaultDay = new DayContext(WorkLocation.Home, BikeCommute: false);
        var days = Enum.GetValues<Weekday>().ToDictionary(weekday => weekday, _ => defaultDay);

        return new WeekContext(
            householdId,
            new AlternatingWeekConfig(referenceWeekStartDate, WeekMode.Solo),
            [],
            days);
    }

    /// <summary>
    /// Rehydrates a <see cref="WeekContext"/> from persisted state.
    /// </summary>
    public static WeekContext Rehydrate(
        Guid householdId,
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days)
    {
        return new WeekContext(householdId, alternatingWeekConfig, weekModeOverrides, days);
    }

    public void Replace(
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days)
    {
        AlternatingWeekConfig = alternatingWeekConfig;

        _weekModeOverrides.Clear();
        _weekModeOverrides.AddRange(weekModeOverrides);

        _days.Clear();
        foreach (var (weekday, dayContext) in days)
        {
            _days[weekday] = dayContext;
        }
    }
}
