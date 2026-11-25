using LifeOS.Application.Common;

namespace LifeOS.Application.Budget;

/// <summary>Budget months are those of Europe/Paris (ADR 0020), whatever the server's time zone.</summary>
public static class BudgetClock
{
    public static DateOnly Today(TimeProvider clock) => ParisTime.Today(clock);
}
