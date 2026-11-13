namespace LifeOS.Application.Sports;

/// <summary>Sport days are those of Europe/Paris (ADR 0013), whatever the server's time zone.</summary>
public static class SportClock
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
