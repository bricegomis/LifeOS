namespace LifeOS.Application.Sports;

/// <summary>Sport days are those of Europe/Paris (ADR 0013), whatever the server's time zone.</summary>
public static class SportClock
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

    /// <summary>A Paris time of a sport day, as a UTC instant (PostgreSQL stores timestamps with time zone in UTC only).</summary>
    public static DateTimeOffset At(DateOnly day, TimeOnly time)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
