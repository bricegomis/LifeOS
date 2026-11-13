namespace LifeOS.Domain.Sports;

/// <summary>Weeks run Monday to Sunday (ADR 0013).</summary>
public static class SportWeek
{
    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
