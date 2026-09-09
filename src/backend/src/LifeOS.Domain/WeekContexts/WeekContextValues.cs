namespace LifeOS.Domain.WeekContexts;

/// <summary>
/// Whether a week should follow the "kids" or "solo" plan, mirroring the frontend's
/// <c>WeekMode</c> union.
/// </summary>
public enum WeekMode
{
    Kids,
    Solo,
}

/// <summary>
/// Where the user works on a given day, mirroring the frontend's <c>WorkLocation</c> union.
/// </summary>
public enum WorkLocation
{
    Home,
    Office,
    Off,
}

/// <summary>
/// Per-day work/commute configuration, mirroring the frontend's <c>DayContext</c> model.
/// </summary>
public sealed record DayContext(WorkLocation WorkLocation, bool BikeCommute);

/// <summary>
/// Reference point used to compute which <see cref="WeekMode"/> applies to any given week,
/// mirroring the frontend's <c>AlternatingWeekConfig</c> model.
/// </summary>
public sealed record AlternatingWeekConfig(DateOnly ReferenceWeekStartDate, WeekMode ReferenceWeekMode);

/// <summary>
/// A one-off override of the computed <see cref="WeekMode"/> for a specific week, mirroring the
/// frontend's <c>WeekModeOverride</c> model.
/// </summary>
public sealed record WeekModeOverride(DateOnly WeekStartDate, WeekMode Mode);
