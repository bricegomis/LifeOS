namespace LifeOS.Application.Common;

/// <summary>
/// LifeOS days are those of Europe/Paris, whatever the server's time zone. Each module keeps its
/// own clock (<c>FoodClock</c>, <c>SportClock</c>…) on top of this one.
/// </summary>
public static class ParisTime
{
    private static readonly TimeZoneInfo Zone = FindZone();

    /// <summary>Today's Paris day.</summary>
    public static DateOnly Today(TimeProvider clock) => DayOf(clock.GetUtcNow());

    /// <summary>A Paris time of a day, as a UTC instant (PostgreSQL stores timestamps with time zone in UTC only).</summary>
    public static DateTimeOffset At(DateOnly day, TimeOnly time)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>The Paris day of an instant.</summary>
    public static DateOnly DayOf(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>The Paris time of an instant.</summary>
    public static TimeOnly TimeOf(DateTimeOffset instant) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
