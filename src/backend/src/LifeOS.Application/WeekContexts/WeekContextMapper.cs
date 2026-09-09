using LifeOS.Domain.Common;
using LifeOS.Domain.WeekContexts;

namespace LifeOS.Application.WeekContexts;

/// <summary>
/// Maps <see cref="WeekContext"/> aggregates to/from their API read model and request payloads.
/// </summary>
internal static class WeekContextMapper
{
    public static WeekContextDto ToDto(WeekContext weekContext)
    {
        var days = weekContext.Days.ToDictionary(
            pair => pair.Key.ToString().ToLowerInvariant(),
            pair => new DayContextDto(pair.Value.WorkLocation.ToString().ToLowerInvariant(), pair.Value.BikeCommute));

        var overrides = weekContext.WeekModeOverrides
            .Select(o => new WeekModeOverrideDto(o.WeekStartDate.ToString("yyyy-MM-dd"), o.Mode.ToString().ToLowerInvariant()))
            .ToList();

        var alternatingWeekConfig = new AlternatingWeekConfigDto(
            weekContext.AlternatingWeekConfig.ReferenceWeekStartDate.ToString("yyyy-MM-dd"),
            weekContext.AlternatingWeekConfig.ReferenceWeekMode.ToString().ToLowerInvariant());

        return new WeekContextDto(alternatingWeekConfig, overrides, days);
    }

    public static AlternatingWeekConfig ParseAlternatingWeekConfig(AlternatingWeekConfigDto dto) =>
        new(DateOnly.Parse(dto.ReferenceWeekStartDate), ParseWeekMode(dto.ReferenceWeekMode));

    public static List<WeekModeOverride> ParseWeekModeOverrides(IEnumerable<WeekModeOverrideDto> overrides) =>
        overrides.Select(o => new WeekModeOverride(DateOnly.Parse(o.WeekStartDate), ParseWeekMode(o.Mode))).ToList();

    public static Dictionary<Weekday, DayContext> ParseDays(IReadOnlyDictionary<string, DayContextDto> days) =>
        days.ToDictionary(
            pair => ParseWeekday(pair.Key),
            pair => new DayContext(ParseWorkLocation(pair.Value.WorkLocation), pair.Value.BikeCommute));

    private static WeekMode ParseWeekMode(string mode) =>
        Enum.TryParse<WeekMode>(mode, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown week mode '{mode}'.", nameof(mode));

    private static WorkLocation ParseWorkLocation(string workLocation) =>
        Enum.TryParse<WorkLocation>(workLocation, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown work location '{workLocation}'.", nameof(workLocation));

    private static Weekday ParseWeekday(string weekday) =>
        Enum.TryParse<Weekday>(weekday, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown weekday '{weekday}'.", nameof(weekday));
}
