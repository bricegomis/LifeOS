namespace LifeOS.Application.WeekContexts;

/// <summary>
/// Read model returned by the API for a day's work/commute settings, mirroring the frontend's
/// <c>DayContext</c> shape.
/// </summary>
public sealed record DayContextDto(string WorkLocation, bool BikeCommute);

/// <summary>
/// Read model returned by the API for the alternating week reference configuration, mirroring the
/// frontend's <c>AlternatingWeekConfig</c> shape.
/// </summary>
public sealed record AlternatingWeekConfigDto(string ReferenceWeekStartDate, string ReferenceWeekMode);

/// <summary>
/// Read model returned by the API for a one-off week mode override, mirroring the frontend's
/// <c>WeekModeOverride</c> shape.
/// </summary>
public sealed record WeekModeOverrideDto(string WeekStartDate, string Mode);

/// <summary>
/// Read model returned by the API for the current user's week context, mirroring the frontend's
/// <c>WeekContext</c> shape.
/// </summary>
public sealed record WeekContextDto(
    AlternatingWeekConfigDto AlternatingWeekConfig,
    IReadOnlyList<WeekModeOverrideDto> WeekModeOverrides,
    IReadOnlyDictionary<string, DayContextDto> Days);
