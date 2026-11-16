using LifeOS.Application.Sports;

namespace LifeOS.Application.Tests.Sports;

public sealed class SportClockTests
{
    [Fact]
    public void A_Paris_time_becomes_a_UTC_instant()
    {
        var winter = SportClock.At(new DateOnly(2026, 11, 16), new TimeOnly(18, 30));
        Assert.Equal(TimeSpan.Zero, winter.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 17, 30, 0, TimeSpan.Zero), winter);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), SportClock.At(new DateOnly(2026, 7, 1), new TimeOnly(12, 0)));
    }
}
