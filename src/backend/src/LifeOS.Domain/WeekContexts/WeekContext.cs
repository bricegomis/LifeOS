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
    private Dictionary<WeekMode, Dictionary<Weekday, DayContext>> _templates = [];
    private List<WeekModeOverride> _weekModeOverrides = [];

    public Guid HouseholdId { get; private set; }
    public AlternatingWeekConfig AlternatingWeekConfig { get; private set; } = null!;
    public IReadOnlyList<WeekModeOverride> WeekModeOverrides => _weekModeOverrides;
    public IReadOnlyDictionary<Weekday, DayContext> Days => _days;
    public IReadOnlyDictionary<WeekMode, Dictionary<Weekday, DayContext>> Templates => _templates;

    private WeekContext()
    {
    }

    private WeekContext(
        Guid householdId,
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days,
        IReadOnlyDictionary<WeekMode, Dictionary<Weekday, DayContext>> templates)
    {
        HouseholdId = householdId;
        AlternatingWeekConfig = alternatingWeekConfig;
        _weekModeOverrides = [.. weekModeOverrides];
        _days = new Dictionary<Weekday, DayContext>(days);
        _templates = NormalizeTemplates(days, templates);
    }

    public static WeekContext CreateDefault(Guid householdId, DateOnly referenceWeekStartDate)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("A week context must belong to a household.", nameof(householdId));
        }

        var defaultDay = new DayContext(WorkLocation.Home, BikeCommute: false);
        var days = Enum.GetValues<Weekday>().ToDictionary(weekday => weekday, _ => defaultDay);

        var templates = Enum.GetValues<WeekMode>().ToDictionary(
            mode => mode,
            _ => new Dictionary<Weekday, DayContext>(days));

        return new WeekContext(
            householdId,
            new AlternatingWeekConfig(referenceWeekStartDate, WeekMode.Solo),
            [],
            days,
            templates);
    }

    /// <summary>
    /// Rehydrates a <see cref="WeekContext"/> from persisted state.
    /// </summary>
    public static WeekContext Rehydrate(
        Guid householdId,
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days,
        IReadOnlyDictionary<WeekMode, Dictionary<Weekday, DayContext>>? templates = null)
    {
        return new WeekContext(
            householdId,
            alternatingWeekConfig,
            weekModeOverrides,
            days,
            NormalizeTemplates(days, templates));
    }

    public void Replace(
        AlternatingWeekConfig alternatingWeekConfig,
        IEnumerable<WeekModeOverride> weekModeOverrides,
        IReadOnlyDictionary<Weekday, DayContext> days,
        IReadOnlyDictionary<WeekMode, Dictionary<Weekday, DayContext>>? templates = null)
    {
        AlternatingWeekConfig = alternatingWeekConfig;

        _weekModeOverrides.Clear();
        _weekModeOverrides.AddRange(weekModeOverrides);

        _templates = NormalizeTemplates(days, templates);

        var referenceDays = _templates[alternatingWeekConfig.ReferenceWeekMode];
        _days.Clear();
        foreach (var (weekday, dayContext) in referenceDays)
        {
            _days[weekday] = dayContext;
        }
    }

    private static Dictionary<WeekMode, Dictionary<Weekday, DayContext>> NormalizeTemplates(
        IReadOnlyDictionary<Weekday, DayContext> days,
        IReadOnlyDictionary<WeekMode, Dictionary<Weekday, DayContext>>? templates) =>
        Enum.GetValues<WeekMode>().ToDictionary(
            mode => mode,
            mode =>
            {
                var sourceDays = templates?.GetValueOrDefault(mode) ?? days;
                return Enum.GetValues<Weekday>().ToDictionary(
                    weekday => weekday,
                    weekday => sourceDays.GetValueOrDefault(weekday) ?? new DayContext(WorkLocation.Home, BikeCommute: false));
            });
}
