namespace LifeOS.Application.Tests.Food;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public static readonly FixedClock Default = new(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));
    public static DateOnly Today => DateOnly.FromDateTime(Default.GetUtcNow().UtcDateTime);
    public override DateTimeOffset GetUtcNow() => now;
}
