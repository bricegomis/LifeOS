using LifeOS.Domain.Sports;

namespace LifeOS.Domain.Tests.Sports;

public sealed class SportSessionTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static SportSessionValues Run(int? calories = 520, decimal? distance = 8.2m) =>
        new(Today, "running", null, 46, distance, calories, 7, 148, 171, " Fractionné ");

    [Fact]
    public void A_session_keeps_its_typed_values_and_unknown_calories_stay_unknown()
    {
        var session = SportSession.Create(Household, Profile, Run(calories: null), Today, Now);
        Assert.Null(session.Calories);
        Assert.Equal(8.2m, session.DistanceKm);
        Assert.Equal("Fractionné", session.Notes);
        Assert.Equal("manual", session.Source);
    }

    [Theory]
    [InlineData("yoga")]
    [InlineData("")]
    public void The_sport_must_come_from_the_fixed_list(string sport) =>
        Assert.Throws<ArgumentException>(() => SportSession.Create(Household, Profile, Run() with { Sport = sport }, Today, Now));

    [Fact]
    public void Only_endurance_sports_carry_a_distance() =>
        Assert.Throws<ArgumentException>(() => SportSession.Create(Household, Profile,
            Run() with { Sport = "strength", DistanceKm = 3 }, Today, Now));

    [Fact]
    public void A_logged_session_is_never_in_the_future() =>
        Assert.Throws<ArgumentException>(() => SportSession.Create(Household, Profile, Run() with { Date = Today.AddDays(1) }, Today, Now));

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(11, null, null)]
    [InlineData(5, 180, 150)]
    [InlineData(5, 20, null)]
    public void Feeling_and_heart_rates_are_checked(int feeling, int? average, int? max) =>
        Assert.Throws<ArgumentException>(() => SportSession.Create(Household, Profile,
            Run() with { Feeling = feeling, AverageHeartRate = average, MaxHeartRate = max }, Today, Now));

    [Fact]
    public void A_template_suggests_values_only_for_its_sport()
    {
        var template = SportTemplate.Create(Household, "Footing 45 min", "running", 45, 8, 500);
        Assert.Equal(500, template.Calories);
        Assert.Throws<ArgumentException>(() => template.Update("Muscu", "strength", 50, 5, 300));
        Assert.Throws<ArgumentException>(() => SportTemplate.Create(Household, "Footing", "run", 45, null, 500));
    }
}
