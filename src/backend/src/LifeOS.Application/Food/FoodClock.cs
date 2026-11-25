using LifeOS.Application.Common;

namespace LifeOS.Application.Food;

/// <summary>Food days (and the season month) are those of Europe/Paris, whatever the server's time zone.</summary>
public static class FoodClock
{
    public static DateOnly Today(TimeProvider clock) => ParisTime.Today(clock);
}
