namespace LifeOS.Domain.Food.Foods;

/// <summary>Nutrition per nutrition base of a food. Every value is optional; zero is a real value.</summary>
public sealed class Nutrition
{
    public double? CaloriesPerUnit { get; init; }
    public double? ProteinsPerUnit { get; init; }
    public double? CarbsPerUnit { get; init; }
    public double? FatsPerUnit { get; init; }
    public double? FibersPerUnit { get; init; }

    private Nutrition() { }

    public Nutrition(double? caloriesPerUnit, double? proteinsPerUnit, double? carbsPerUnit, double? fatsPerUnit,
        double? fibersPerUnit = null)
    {
        foreach (var value in new[] { caloriesPerUnit, proteinsPerUnit, carbsPerUnit, fatsPerUnit, fibersPerUnit })
            if (value is not null && (!double.IsFinite(value.Value) || value < 0 || value > 100000))
                throw new ArgumentException("Les valeurs nutritionnelles doivent être finies, positives ou nulles.");
        CaloriesPerUnit = caloriesPerUnit;
        ProteinsPerUnit = proteinsPerUnit;
        CarbsPerUnit = carbsPerUnit;
        FatsPerUnit = fatsPerUnit;
        FibersPerUnit = fibersPerUnit;
    }
}
