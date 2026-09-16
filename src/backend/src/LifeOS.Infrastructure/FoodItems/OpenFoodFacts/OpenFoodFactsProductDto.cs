using System.Text.Json.Serialization;

namespace LifeOS.Infrastructure.FoodItems.OpenFoodFacts;

/// <summary>
/// Minimal DTO for Open Food Facts API responses (Jalon 3).
/// </summary>
public sealed class OpenFoodFactsProductDto
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("generic_name")]
    public string? GenericName { get; set; }

    [JsonPropertyName("nutriments")]
    public Dictionary<string, double?>? Nutriments { get; set; }

    [JsonPropertyName("serving_quantity")]
    public double? ServingQuantity { get; set; }

    [JsonPropertyName("serving_unit")]
    public string? ServingUnit { get; set; }

    /// <summary>
    /// Returns the product name, falling back to generic name if not available.
    /// </summary>
    public string GetName() => !string.IsNullOrWhiteSpace(ProductName) ? ProductName : (GenericName ?? "Unknown Product");

    /// <summary>
    /// Extracts nutrition per 100g from the nutriments field.
    /// </summary>
    public (double? cal, double? protein, double? carbs, double? fat) GetNutrients()
    {
        if (Nutriments == null)
            return (null, null, null, null);

        var calories = Nutriments.TryGetValue("energy-kcal_100g", out var kcal) ? kcal : null;
        var protein = Nutriments.TryGetValue("proteins_100g", out var prot) ? prot : null;
        var carbs = Nutriments.TryGetValue("carbohydrates_100g", out var carb) ? carb : null;
        var fat = Nutriments.TryGetValue("fat_100g", out var f) ? f : null;

        return (calories, protein, carbs, fat);
    }
}

/// <summary>
/// Open Food Facts API search response wrapper.
/// </summary>
public sealed class OpenFoodFactsSearchResponse
{
    [JsonPropertyName("products")]
    public List<OpenFoodFactsProductDto> Products { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
