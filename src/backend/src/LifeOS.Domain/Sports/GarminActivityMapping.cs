namespace LifeOS.Domain.Sports;

/// <summary>
/// How Garmin's values become LifeOS ones (ADR 0019): a fixed table from the Garmin activity type to
/// a sport of <see cref="SportCatalog"/>, and the watch's end-of-session questions. Every activity is
/// imported; an unknown type becomes <c>other</c> and keeps its Garmin type.
/// </summary>
public static class GarminActivityMapping
{
    private static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["running"] = "running", ["trail_running"] = "running", ["treadmill_running"] = "running",
        ["track_running"] = "running", ["indoor_running"] = "running", ["street_running"] = "running",
        ["virtual_run"] = "running", ["ultra_run"] = "running",
        ["walking"] = "walking", ["casual_walking"] = "walking", ["speed_walking"] = "walking", ["indoor_walking"] = "walking",
        ["hiking"] = "hiking", ["mountaineering"] = "hiking",
        ["cycling"] = "cycling", ["road_biking"] = "cycling", ["mountain_biking"] = "cycling", ["gravel_cycling"] = "cycling",
        ["indoor_cycling"] = "cycling", ["virtual_ride"] = "cycling", ["cyclocross"] = "cycling", ["e_bike_fitness"] = "cycling",
        ["e_bike_mountain"] = "cycling", ["bmx"] = "cycling", ["recumbent_cycling"] = "cycling", ["track_cycling"] = "cycling",
        ["lap_swimming"] = "swimming", ["open_water_swimming"] = "swimming", ["swimming"] = "swimming",
        ["strength_training"] = "strength",
        ["hiit"] = "crossfit",
        ["yoga"] = "mobility", ["pilates"] = "mobility", ["breathwork"] = "mobility", ["stretching"] = "mobility",
        ["tennis"] = "racket", ["padel"] = "racket", ["badminton"] = "racket", ["squash"] = "racket",
        ["table_tennis"] = "racket", ["pickleball"] = "racket",
        ["soccer"] = "team_sport", ["basketball"] = "team_sport", ["volleyball"] = "team_sport", ["rugby"] = "team_sport",
    };

    public static string SportFor(string? garminType) =>
        garminType is not null && Types.TryGetValue(garminType.Trim(), out var sport) ? sport : "other";

    /// <summary>Garmin stores the perceived effort as 10 to 100; LifeOS keeps 1 to 10.</summary>
    public static int? Effort(int? directWorkoutRpe) =>
        directWorkoutRpe is { } rpe and > 0 ? Math.Clamp((int)Math.Round(rpe / 10m), 1, 10) : null;

    /// <summary>Garmin stores the feeling as 0, 25, 50, 75 or 100: the same five levels.</summary>
    public static string? Feel(int? directWorkoutFeel) => directWorkoutFeel switch
    {
        null => null,
        < 13 => "very_weak",
        < 38 => "weak",
        < 63 => "normal",
        < 88 => "strong",
        _ => "very_strong",
    };
}
