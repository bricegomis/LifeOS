using LifeOS.Domain.Households;
using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Domain.Tests.Nutrition;

public class UserConfigurationTests
{
    [Fact]
    public void ComputeDailyFoodTarget_WithActivityEnergy_CalculatesCorrectly()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var config = UserConfiguration.Create(
            householdId,
            dailyBaseEnergyKcal: 2000m,
            targetNetDeficitKcal: 500m,
            targetProteinG: 150m,
            targetCarbsG: 200m,
            targetFatsG: 70m);

        var activityEnergyKcal = 400m;

        // Act
        // Formula: daily_food_target = base_energy + activity_energy - deficit_goal
        var result = config.ComputeDailyFoodTarget(activityEnergyKcal);

        // Assert
        // 2000 + 400 - 500 = 1900
        Assert.Equal(1900m, result);
    }

    [Fact]
    public void ComputeDailyFoodTarget_WithoutActivityEnergy_CalculatesCorrectly()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var config = UserConfiguration.Create(
            householdId,
            dailyBaseEnergyKcal: 2000m,
            targetNetDeficitKcal: 500m,
            targetProteinG: 150m,
            targetCarbsG: 200m,
            targetFatsG: 70m);

        var activityEnergyKcal = 0m;

        // Act
        // Formula: daily_food_target = base_energy + activity_energy - deficit_goal
        var result = config.ComputeDailyFoodTarget(activityEnergyKcal);

        // Assert
        // 2000 + 0 - 500 = 1500
        Assert.Equal(1500m, result);
    }

    [Fact]
    public void ComputeDailyFoodTarget_WithLargeActivityEnergy_CalculatesCorrectly()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var config = UserConfiguration.Create(
            householdId,
            dailyBaseEnergyKcal: 2000m,
            targetNetDeficitKcal: 500m,
            targetProteinG: 150m,
            targetCarbsG: 200m,
            targetFatsG: 70m);

        var activityEnergyKcal = 1000m;

        // Act
        // Formula: daily_food_target = base_energy + activity_energy - deficit_goal
        var result = config.ComputeDailyFoodTarget(activityEnergyKcal);

        // Assert
        // 2000 + 1000 - 500 = 2500
        Assert.Equal(2500m, result);
    }

    [Fact]
    public void UpdateConfiguration_UpdatesAllFields()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var config = UserConfiguration.Create(
            householdId,
            dailyBaseEnergyKcal: 2000m,
            targetNetDeficitKcal: 500m,
            targetProteinG: 150m,
            targetCarbsG: 200m,
            targetFatsG: 70m);

        // Act
        config.UpdateConfiguration(
            dailyBaseEnergyKcal: 2200m,
            targetNetDeficitKcal: 600m,
            targetProteinG: 160m,
            targetCarbsG: 210m,
            targetFatsG: 75m);

        // Assert
        Assert.Equal(2200m, config.DailyBaseEnergyKcal);
        Assert.Equal(600m, config.TargetNetDeficitKcal);
        Assert.Equal(160m, config.TargetProteinG);
        Assert.Equal(210m, config.TargetCarbsG);
        Assert.Equal(75m, config.TargetFatsG);
    }

    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        // Act
        var config = UserConfiguration.Create(
            Guid.NewGuid(),
            dailyBaseEnergyKcal: 2000m,
            targetNetDeficitKcal: 500m,
            targetProteinG: 150m,
            targetCarbsG: 200m,
            targetFatsG: 70m);

        // Assert
        Assert.NotEqual(Guid.Empty, config.Id);
        Assert.Equal(2000m, config.DailyBaseEnergyKcal);
        Assert.Equal(500m, config.TargetNetDeficitKcal);
        Assert.Equal(150m, config.TargetProteinG);
        Assert.Equal(200m, config.TargetCarbsG);
        Assert.Equal(70m, config.TargetFatsG);
    }

    [Fact]
    public void Create_WithEmptyHouseholdId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            UserConfiguration.Create(
                Guid.Empty,
                dailyBaseEnergyKcal: 2000m,
                targetNetDeficitKcal: 500m,
                targetProteinG: 150m,
                targetCarbsG: 200m,
                targetFatsG: 70m));
    }

    [Fact]
    public void Create_WithNegativeEnergyValues_ThrowsOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UserConfiguration.Create(
                Guid.NewGuid(),
                dailyBaseEnergyKcal: -100m,
                targetNetDeficitKcal: 500m,
                targetProteinG: 150m,
                targetCarbsG: 200m,
                targetFatsG: 70m));
    }
}

public class ActivitySessionTests
{
    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        // Act
        var session = ActivitySession.Create(
            Guid.NewGuid(),
            type: "run",
            intensity: "moderate",
            durationMinutes: 30,
            estimatedEnergyKcal: 300m);

        // Assert
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal("run", session.Type);
        Assert.Equal("moderate", session.Intensity);
        Assert.Equal(30, session.DurationMinutes);
        Assert.Equal(300m, session.EstimatedEnergyKcal);
    }

    [Fact]
    public void Create_WithEmptyDayPlanId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ActivitySession.Create(
                Guid.Empty,
                type: "run",
                intensity: "moderate",
                durationMinutes: 30,
                estimatedEnergyKcal: 300m));
    }

    [Fact]
    public void Create_WithNegativeDuration_ThrowsOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivitySession.Create(
                Guid.NewGuid(),
                type: "run",
                intensity: "moderate",
                durationMinutes: -10,
                estimatedEnergyKcal: 300m));
    }

    [Fact]
    public void Create_WithNegativeEnergy_ThrowsOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivitySession.Create(
                Guid.NewGuid(),
                type: "run",
                intensity: "moderate",
                durationMinutes: 30,
                estimatedEnergyKcal: -100m));
    }

    [Fact]
    public void UpdateDetails_UpdatesAllFields()
    {
        // Arrange
        var session = ActivitySession.Create(
            Guid.NewGuid(),
            type: "run",
            intensity: "moderate",
            durationMinutes: 30,
            estimatedEnergyKcal: 300m);

        // Act
        session.UpdateDetails(
            type: "bike",
            intensity: "high",
            durationMinutes: 45,
            estimatedEnergyKcal: 400m);

        // Assert
        Assert.Equal("bike", session.Type);
        Assert.Equal("high", session.Intensity);
        Assert.Equal(45, session.DurationMinutes);
        Assert.Equal(400m, session.EstimatedEnergyKcal);
    }

    [Fact]
    public void UpdateDetails_WithPartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var session = ActivitySession.Create(
            Guid.NewGuid(),
            type: "run",
            intensity: "moderate",
            durationMinutes: 30,
            estimatedEnergyKcal: 300m);

        // Act
        session.UpdateDetails(durationMinutes: 45);

        // Assert
        Assert.Equal("run", session.Type); // Unchanged
        Assert.Equal("moderate", session.Intensity); // Unchanged
        Assert.Equal(45, session.DurationMinutes); // Updated
        Assert.Equal(300m, session.EstimatedEnergyKcal); // Unchanged
    }
}
