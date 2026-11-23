using LifeOS.Domain.Sports;

namespace LifeOS.Domain.Tests.Sports;

public sealed class SportSessionTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 17, 0, 0, TimeSpan.Zero);

    private static SportSessionValues Run(int? calories = 520, decimal? distance = 8.2m) =>
        new(Today, Start, Start.AddMinutes(46), "running", null, 46, distance, calories, 7, "strong", 148, 171, " Fractionné ");

    private static GarminActivityValues Activity(string type = "running", int effort = 6, DateTimeOffset? start = null)
    {
        var begin = start ?? Start.AddMinutes(5);
        return new GarminActivityValues(42, type, "Course à pied", GarminActivityMapping.SportFor(type), Today, begin, begin.AddMinutes(30), 28,
            4.68m, 331, effort, "normal", 133, 150, 41, 10.03m, 2.7m, 0.8m, [180, 540, 840, 120, 0], "{\"activityId\":42}");
    }

    [Fact]
    public void A_session_keeps_its_typed_values_and_unknown_calories_stay_unknown()
    {
        var session = SportSession.Create(Household, Profile, Run(calories: null), Today, Now);
        Assert.Null(session.Calories);
        Assert.Equal(8.2m, session.DistanceKm);
        Assert.Equal("Fractionné", session.Notes);
        Assert.Equal("manual", session.Source);
        Assert.Equal(Start, session.StartedAt);
        Assert.Equal("strong", session.Feel);
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

    [Fact]
    public void The_end_follows_the_start_within_a_day()
    {
        Assert.Throws<ArgumentException>(() => SportSession.Create(Household, Profile, Run() with { EndedAt = Start }, Today, Now));
        Assert.Throws<ArgumentException>(() => SportSession.Create(Household, Profile, Run() with { EndedAt = Start.AddHours(25) }, Today, Now));
    }

    [Theory]
    [InlineData(0, null, null, null)]
    [InlineData(11, null, null, null)]
    [InlineData(5, "great", null, null)]
    [InlineData(5, null, 180, 150)]
    [InlineData(5, null, 20, null)]
    public void Effort_feel_and_heart_rates_are_checked(int effort, string? feel, int? average, int? max) =>
        Assert.Throws<ArgumentException>(() => SportSession.Create(Household, Profile,
            Run() with { Effort = effort, Feel = feel, AverageHeartRate = average, MaxHeartRate = max }, Today, Now));

    [Fact]
    public void A_template_suggests_values_only_for_its_sport()
    {
        var template = SportTemplate.Create(Household, "Footing 45 min", "running", 45, 8, 500);
        Assert.Equal(500, template.Calories);
        Assert.Throws<ArgumentException>(() => template.Update("Muscu", "strength", 50, 5, 300));
        Assert.Throws<ArgumentException>(() => SportTemplate.Create(Household, "Footing", "run", 45, null, 500));
    }

    [Fact]
    public void Garmin_values_overwrite_but_notes_and_a_changed_sport_stay()
    {
        var session = SportSession.FromGarmin(Household, Profile, Activity("hiit"), Today, Now);
        Assert.Equal("crossfit", session.Sport);
        Assert.Equal("garmin", session.Source);
        Assert.Equal(2.7m, session.AerobicTrainingEffect);

        session.Update(new SportSessionValues(Today, Start, Start.AddMinutes(1), "strength", null, 1, null, null, 1, null, null, null, "WOD Cindy"), Today, Now);
        Assert.Equal("strength", session.Sport);
        Assert.Equal(28, session.DurationMinutes);
        Assert.Equal("WOD Cindy", session.Notes);

        session.ApplyGarmin(Activity("hiit", effort: 9), Today, Now);
        Assert.Equal("strength", session.Sport);
        Assert.Equal(9, session.Effort);
        Assert.Equal("WOD Cindy", session.Notes);
    }

    [Fact]
    public void A_manual_session_merges_with_the_overlapping_activity_of_the_same_sport()
    {
        var manual = SportSession.Create(Household, Profile, Run(), Today, Now);
        Assert.True(manual.MatchesGarmin("running", Start.AddMinutes(5), Start.AddMinutes(35)));
        Assert.False(manual.MatchesGarmin("cycling", Start.AddMinutes(5), Start.AddMinutes(35)));
        Assert.False(manual.MatchesGarmin("running", Start.AddHours(2), Start.AddHours(3)));

        manual.ApplyGarmin(Activity(), Today, Now);
        Assert.Equal("garmin", manual.Source);
        Assert.Equal("Fractionné", manual.Notes);
        Assert.Equal(4.68m, manual.DistanceKm);
        Assert.False(manual.MatchesGarmin("running", Start, Start.AddMinutes(30)));
    }

    [Theory]
    [InlineData("trail_running", "running")]
    [InlineData("open_water_swimming", "swimming")]
    [InlineData("lap_swimming", "swimming")]
    [InlineData("hiit", "crossfit")]
    [InlineData("hiking", "hiking")]
    [InlineData("indoor_cycling", "cycling")]
    [InlineData("skate_skiing_ws", "other")]
    public void Every_garmin_type_maps_to_a_sport(string type, string sport) =>
        Assert.Equal(sport, GarminActivityMapping.SportFor(type));

    [Fact]
    public void The_watch_questions_keep_their_scale()
    {
        Assert.Equal(7, GarminActivityMapping.Effort(70));
        Assert.Null(GarminActivityMapping.Effort(null));
        Assert.Equal("very_weak", GarminActivityMapping.Feel(0));
        Assert.Equal("normal", GarminActivityMapping.Feel(50));
        Assert.Equal("very_strong", GarminActivityMapping.Feel(100));
    }
}
