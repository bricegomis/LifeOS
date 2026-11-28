using LifeOS.Application.Common;

namespace LifeOS.Application.Reading;

/// <summary>Reading days are those of Europe/Paris (ADR 0022), whatever the server's time zone.</summary>
public static class ReadingClock
{
    public static DateOnly Today(TimeProvider clock) => ParisTime.Today(clock);
}
