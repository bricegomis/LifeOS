using LifeOS.Domain.Health;

namespace LifeOS.Domain.Tests.Health;

public sealed class SupplementTests
{
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static Supplement Create(SupplementPeriod period, decimal dose = 2, decimal content = 120, decimal? price = 24m) =>
        Supplement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " Magnésium ", null, dose, "gelule", "soir", content, price,
            "Pharmacie", [("Pharmacie du centre", "https://www.example.fr/magnesium"), (null, "https://www.shop.fr/x")], period, Now);

    [Fact]
    public void The_monthly_cost_follows_the_dose_and_the_share_of_days_in_cure()
    {
        Assert.Equal(12m, Create(SupplementPeriod.AllYear(Today)).MonthlyCost); // 24 × 2 × 30 / 120
        // Cure of 3 months then 1 month of pause: three quarters of the time.
        Assert.Equal(9m, Create(SupplementPeriod.Cure(Today, 3, "month", 1, "month")).MonthlyCost);
        Assert.Equal(12m, Create(SupplementPeriod.Cure(Today, 2, "month", 0, "month")).MonthlyCost);
        Assert.Null(Create(SupplementPeriod.AllYear(Today), price: null).MonthlyCost);
    }

    [Fact]
    public void A_repeating_cure_alternates_cure_and_pause()
    {
        var period = SupplementPeriod.Cure(Today, 1, "month", 2, "week");
        Assert.Equal((true, 1, 30), (period.StatusOn(Today).Active, period.StatusOn(Today).DayInCure!.Value, period.StatusOn(Today).DaysLeft!.Value));
        var pause = period.StatusOn(Today.AddDays(32));
        Assert.False(pause.Active);
        Assert.Equal(Today.AddDays(44), pause.NextCureOn);
        Assert.True(period.StatusOn(Today.AddDays(44)).Active);
        Assert.True(SupplementPeriod.Cure(Today, 1, "week", 0, "week").StatusOn(Today.AddDays(7)).Ended);
        Assert.False(period.StatusOn(Today.AddDays(-1)).Active);
    }

    [Fact]
    public void Links_are_https_and_named_after_their_site_by_default()
    {
        var supplement = Create(SupplementPeriod.AllYear(Today));
        Assert.Equal(["Pharmacie du centre", "shop.fr"], supplement.OrderedLinks.Select(l => l.Label));
        Assert.Throws<ArgumentException>(() => Supplement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", null, 1, "g", null, 100, null, null,
            [(null, "http://example.fr")], SupplementPeriod.AllYear(Today), Now));
    }

    [Theory]
    [InlineData("pilule")]
    [InlineData("")]
    public void The_unit_comes_from_the_fixed_list(string unit) =>
        Assert.Throws<ArgumentException>(() => Supplement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", null, 1, unit, null, 100, null, null,
            null, SupplementPeriod.AllYear(Today), Now));

    [Fact]
    public void One_tick_per_day_builds_a_streak_that_skips_days_out_of_cure()
    {
        var supplement = Create(SupplementPeriod.AllYear(Today.AddDays(-10)));
        supplement.SetTaken(Today.AddDays(-1), true, Today, Now);
        supplement.SetTaken(Today.AddDays(-2), true, Today, Now);
        supplement.SetTaken(Today.AddDays(-2), true, Today, Now);
        Assert.Equal(2, supplement.Streak(Today));
        supplement.SetTaken(Today, true, Today, Now);
        Assert.Equal(3, supplement.Streak(Today));
        supplement.SetTaken(Today.AddDays(-1), false, Today, Now);
        Assert.Equal(1, supplement.Streak(Today));
        Assert.Throws<ArgumentException>(() => supplement.SetTaken(Today.AddDays(1), true, Today, Now));
        supplement.Stop(Now);
        Assert.Throws<ArgumentException>(() => supplement.SetTaken(Today, false, Today, Now));
    }
}
