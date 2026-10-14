namespace LifeOS.Application.FoodItems;

public sealed class NutritionDto
{
    public double? CaloriesPerUnit { get; set; }
    public double? ProteinsPerUnit { get; set; }
    public double? CarbsPerUnit { get; set; }
    public double? FatsPerUnit { get; set; }
}

public sealed record FoodItemDto(Guid Id, Guid HouseholdId, string Name, bool IsArchived,
    string ReferenceUnit, NutritionDto? Nutrition, string? Fodmap, int? Nova, string? DigestiveNotes,
    IReadOnlyList<string> MissingFields, string Source, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
