using LifeOS.Domain.Common;

namespace LifeOS.Domain.WeekContexts;

/// <summary>
/// A single user's week planning context (alternating week configuration, per-week overrides and
/// per-day work/commute settings), mirroring the frontend's <c>WeekContext</c> model. Aggregate
/// root of the WeekContexts bounded context; there is exactly one per owner.
/// </summary>
public sealed class WeekContext
{
    private readonly Dictionary<Weekday, DayContext> _days;
    private readonly List<WeekModeOverride> _weekModeOverrides;

    public Guid OwnerId { get; private set; }
    public AlternatingWeekConfig AlternatingWeekConfig { get; private set; }
    public IReadOnlyList<WeekModeOverride> WeekModeOverrides => _weekModeOverrides;
    public IReadOnlyDictionary<Weekday, DayContext> Days => _days;

    private WeekContext(
        Guid ownerId,
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days)
    {
        OwnerId = ownerId;
        AlternatingWeekConfig = alternatingWeekConfig;
        _weekModeOverrides = [.. weekModeOverrides];
        _days = new Dictionary<Weekday, DayContext>(days);
    }

    public static WeekContext CreateDefault(Guid ownerId, DateOnly referenceWeekStartDate)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("A week context must belong to an owner.", nameof(ownerId));
        }

        var defaultDay = new DayContext(WorkLocation.Home, BikeCommute: false);
        var days = Enum.GetValues<Weekday>().ToDictionary(weekday => weekday, _ => defaultDay);

        return new WeekContext(
            ownerId,
            new AlternatingWeekConfig(referenceWeekStartDate, WeekMode.Solo),
            [],
            days);
    }

    /// <summary>
    /// Rehydrates a <see cref="WeekContext"/> from persisted state.
    /// </summary>
    public static WeekContext Rehydrate(
        Guid ownerId,
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days)
    {
        return new WeekContext(ownerId, alternatingWeekConfig, weekModeOverrides, days);
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
