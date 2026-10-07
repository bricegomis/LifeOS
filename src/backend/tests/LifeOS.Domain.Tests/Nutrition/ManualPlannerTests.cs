using LifeOS.Domain.FoodItems;
using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Domain.Tests.Nutrition;

public sealed class ManualPlannerTests
{
    [Fact]
    public void Children_only_increase_preparation()
    {
        var meal = PlannedMeal.CreateManual(Guid.NewGuid());
        meal.Schedule(meal.DayPlanId, 990, 1005, 1, 2);
        var line = MealFoodLine.Snapshot(meal.Id, null, "Riz", 100, "g", "100g", new(150, 3, 30, 1));
        Assert.Equal(2, meal.PreparationFactor);
        Assert.Equal(1d, line.NutritionFactor());
        meal.Schedule(meal.DayPlanId, 990, 1005, 2, 0);
        Assert.Equal(1, meal.PreparationFactor);
    }

    [Theory]
    [InlineData("g", "100 g", 100, 1)]
    [InlineData("kg", "100g", 1, 10)]
    [InlineData("ml", "100ml", 200, 2)]
    [InlineData("piece", "1 pièce", 2, 2)]
    public void Known_scales_only(string unit, string reference, int quantity, double expected)
    {
        var line = MealFoodLine.Snapshot(Guid.NewGuid(), null, "Aliment", quantity, unit, reference, null);
        Assert.Equal(expected, line.NutritionFactor());
    }

    [Fact]
    public void No_mass_volume_or_piece_conversion()
    {
        Assert.Null(MealFoodLine.Snapshot(Guid.NewGuid(), null, "Banane", 1, "piece", "100g", null).NutritionFactor());
        Assert.Null(MealFoodLine.Snapshot(Guid.NewGuid(), null, "Lait", 100, "ml", "100g", null).NutritionFactor());
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
