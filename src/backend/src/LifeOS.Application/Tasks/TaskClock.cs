using LifeOS.Application.Common;

namespace LifeOS.Application.Tasks;

/// <summary>Task days are those of Europe/Paris (ADR 0017), whatever the server's time zone.</summary>
public static class TaskClock
{
    public static DateOnly Today(TimeProvider clock) => ParisTime.Today(clock);
}
