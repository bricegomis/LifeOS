namespace LifeOS.Domain.Food;

/// <summary>What a quantity measures. Mass, volume and count are never converted into each other.</summary>
public enum Dimension { Mass, Volume, Count }

/// <summary>A quantity expressed in its dimension's comparison base: kilograms, litres or pieces.</summary>
public readonly record struct BaseQuantity(Dimension Dimension, decimal Amount);

/// <summary>
/// The closed lists of units of the Food module and the only conversions it knows: within a
/// dimension (g ↔ kg, ml ↔ L). No density, piece weight or drained weight is ever inferred.
/// </summary>
public static class Measures
{
    public const string Piece = "pièce";
    public static readonly IReadOnlyList<string> IngredientUnits = ["g", "ml", Piece];
    public static readonly IReadOnlyList<string> PackageUnits = ["g", "kg", "ml", "L", Piece];
    public static readonly IReadOnlyList<string> NutritionBases = ["100 g", "100 ml", "1 pièce"];

    public static BaseQuantity? ToBase(decimal quantity, string unit) => unit switch
    {
        "g" => new(Dimension.Mass, quantity / 1000m),
        "kg" => new(Dimension.Mass, quantity),
        "ml" => new(Dimension.Volume, quantity / 1000m),
        "L" => new(Dimension.Volume, quantity),
        Piece => new(Dimension.Count, quantity),
        _ => null,
    };

    /// <summary>The nutrition base of a food ("100 g", "100 ml", "1 pièce") in comparison units.</summary>
    public static BaseQuantity? NutritionBase(string nutritionBase) => nutritionBase switch
    {
        "100 g" => new(Dimension.Mass, 0.1m),
        "100 ml" => new(Dimension.Volume, 0.1m),
        "1 pièce" => new(Dimension.Count, 1m),
        _ => null,
    };

    public static string BaseUnitLabel(Dimension dimension) => dimension switch
    {
        Dimension.Mass => "kg",
        Dimension.Volume => "L",
        _ => Piece,
    };

    internal static void RequirePositive(decimal quantity, string message)
    {
        if (quantity < 0.000001m || quantity > 1000000 || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentException(message);
    }
}
