namespace LifeOS.Application.FoodItems;

/// <summary>
/// DTO for nutrition information in food item responses.
/// </summary>
public sealed class NutritionDto
{
    public double? CaloriesPerUnit { get; set; }
    public double? ProteinsPerUnit { get; set; }
    public double? CarbsPerUnit { get; set; }
    public double? FatsPerUnit { get; set; }
}

/// <summary>
/// DTO for food item responses (Jalon 3).
/// </summary>
public sealed class FoodItemDto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReferenceUnit { get; set; } = string.Empty;
    public NutritionDto? Nutrition { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? OffBarcode { get; set; }
    public Guid? IsCorrectionOf { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
