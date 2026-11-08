using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Domain.Tests.WeekPlanning;

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
}
