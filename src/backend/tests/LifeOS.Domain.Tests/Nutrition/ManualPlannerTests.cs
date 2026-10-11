using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Domain.Tests.Nutrition;

public sealed class ManualPlannerTests
{
    [Fact]
    public void Planned_recipe_line_uses_explicit_total_or_profile_portions()
    {
        var mealId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var line = PlannedRecipeLine.Create(mealId, householdId, null, 0, null, null, 2, 1);
        Assert.Equal(1, line.TotalServings);

        var profileId = Guid.NewGuid();
        var portion = PlannedRecipeLinePortion.Create(line.Id, householdId, profileId, 0.5m);
        line.SetPortions(null, [portion]);
        Assert.Equal(0.5m, line.TotalServings);
        Assert.Null(line.PlannedServings);
        Assert.Throws<ArgumentException>(() => line.SetPortions(1, [portion]));
    }

    [Fact]
    public void Planned_recipe_line_accepts_fractional_yield_and_servings()
    {
        var line = PlannedRecipeLine.Create(Guid.NewGuid(), Guid.NewGuid(), null, 0,
            "Chia-coco", null, 2, 0.5m);
        Assert.Equal(0.5m, line.TotalServings);
        Assert.Equal(0.25m, line.TotalServings / line.YieldServings);
        Assert.Throws<ArgumentException>(() => PlannedRecipeLine.Create(Guid.NewGuid(), Guid.NewGuid(),
            null, 0, null, null, 0, 1));
    }

    [Fact]
    public void Sport_calories_are_not_scaled_and_out_of_range_time_is_allowed()
    {
        var session = ActivitySession.Create(Guid.NewGuid(), "bike", "moderate", 30, 200);
        session.Schedule(session.DayPlanId, 1320, 1380, "Vélo", "bike", "moderate", 60, 10, 200, null);
        Assert.Equal(200, session.EstimatedEnergyKcal);
        Assert.Throws<ArgumentException>(() => EventTime.Validate(100, 90));
        Assert.Throws<ArgumentException>(() => EventTime.Validate(null, 90));
        EventTime.Validate(null, null);
    }

    [Fact]
    public void Manual_week_has_seven_empty_days_and_valid_timezone()
    {
        var week = Week.CreateManual(Guid.NewGuid(), new(2026, 10, 5), "Europe/Paris");
        Assert.True(week.IsManual);
        Assert.Equal(7, week.DayPlans.Count);
        Assert.All(week.DayPlans, day => { Assert.Empty(day.PlannedMeals); Assert.False(day.BikeCommute); });
        Assert.Throws<ArgumentException>(() => Week.CreateManual(Guid.NewGuid(), new(2026, 10, 6), "Europe/Paris"));
    }

}
