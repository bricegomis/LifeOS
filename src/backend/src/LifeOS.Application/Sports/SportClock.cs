using LifeOS.Application.Common;

namespace LifeOS.Application.Sports;

/// <summary>Sport days are those of Europe/Paris (ADR 0013), whatever the server's time zone.</summary>
public static class SportClock
{
    public static DateOnly Today(TimeProvider clock) => ParisTime.Today(clock);

    /// <summary>A Paris time of a sport day, as a UTC instant (PostgreSQL stores timestamps with time zone in UTC only).</summary>
    public static DateTimeOffset At(DateOnly day, TimeOnly time) => ParisTime.At(day, time);

    /// <summary>The Paris day of an instant.</summary>
    public static DateOnly DayOf(DateTimeOffset instant) => ParisTime.DayOf(instant);

    /// <summary>The Paris time of an instant.</summary>
    public static TimeOnly TimeOf(DateTimeOffset instant) => ParisTime.TimeOf(instant);
}
