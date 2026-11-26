using LifeOS.Domain.Health;

namespace LifeOS.Domain.Tests.Health;

public sealed class MarkerTests
{
    private static readonly DateOnly Today = new(2026, 11, 22);

    private static Marker Ferritin(int frequency = 6) =>
        Marker.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ferritine", "µg/L", 30, 400, 80, 150, frequency, ["Bilan fer", " bilan FER "]);

    [Fact]
    public void A_value_is_placed_against_the_lab_range_and_the_personal_target()
    {
        var marker = Ferritin();
        Assert.Equal(["Bilan fer"], marker.Panels);
        Assert.Equal("below_lab", marker.StatusOf(20));
        Assert.Equal("normal", marker.StatusOf(62));
        Assert.Equal("in_target", marker.StatusOf(88));
        Assert.Equal("above_lab", marker.StatusOf(420));
    }

    [Fact]
    public void The_trend_follows_the_distance_to_the_target()
    {
        var marker = Ferritin();
        Assert.Null(marker.TrendOf(null, 62));
        Assert.Equal("better", marker.TrendOf(45, 62));
        Assert.Equal("worse", marker.TrendOf(62, 45));
        Assert.Equal("stable", marker.TrendOf(62, 63));
        Assert.Equal("in_target", marker.TrendOf(88, 120));
        // Above the target, going down is better.
        Assert.Equal("better", marker.TrendOf(300, 200));
    }

    [Fact]
    public void A_marker_is_due_after_its_frequency_or_when_never_measured()
    {
        Assert.Equal(Today, Ferritin().NextDueOn(null, Today));
        Assert.Equal(new DateOnly(2027, 5, 22), Ferritin().NextDueOn(Today, Today));
        Assert.Null(Ferritin(0).NextDueOn(Today.AddYears(-3), Today));
    }

    [Fact]
    public void Ranges_must_be_ordered() =>
        Assert.Throws<ArgumentException>(() => Marker.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TSH", "mUI/L", 4, 0.4m, null, null, 12, null));

    [Fact]
    public void Explanations_are_trimmed_blank_clears_them_and_length_is_bounded()
    {
        var marker = Ferritin();
        marker.Describe("  Réserves de fer.  ", "Carence possible.", " ");
        Assert.Equal(("Réserves de fer.", "Carence possible.", (string?)null), (marker.Description, marker.WhenLow, marker.WhenHigh));
        marker.Describe(null, "", null);
        Assert.Null(marker.WhenLow);
        Assert.Throws<ArgumentException>(() => marker.Describe(new string('a', Marker.MaxTextLength + 1), null, null));
    }
}
