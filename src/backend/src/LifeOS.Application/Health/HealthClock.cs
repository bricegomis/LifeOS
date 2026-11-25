using LifeOS.Application.Common;

namespace LifeOS.Application.Health;

/// <summary>Health days are those of Europe/Paris (ADR 0018), whatever the server's time zone.</summary>
public static class HealthClock
{
    public static DateOnly Today(TimeProvider clock) => ParisTime.Today(clock);
}
