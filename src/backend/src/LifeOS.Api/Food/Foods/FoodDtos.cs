using System.ComponentModel.DataAnnotations;
using LifeOS.Domain.Food.Foods;

namespace LifeOS.Api.Food.Foods;

public sealed class NutritionDto
{
    public double? CaloriesPerUnit { get; set; }
    public double? ProteinsPerUnit { get; set; }
    public double? CarbsPerUnit { get; set; }
    public double? FatsPerUnit { get; set; }

    public Nutrition ToDomain() => new(CaloriesPerUnit, ProteinsPerUnit, CarbsPerUnit, FatsPerUnit);

    public static NutritionDto? From(Nutrition? nutrition) => nutrition is null ? null : new NutritionDto
    {
        CaloriesPerUnit = nutrition.CaloriesPerUnit, ProteinsPerUnit = nutrition.ProteinsPerUnit,
        CarbsPerUnit = nutrition.CarbsPerUnit, FatsPerUnit = nutrition.FatsPerUnit,
    };
}

public sealed record FoodItemDto(Guid Id, Guid HouseholdId, string Name, bool IsArchived,
    string ReferenceUnit, NutritionDto? Nutrition, string? Fodmap, int? Nova, string? DigestiveNotes,
    bool HasImage, IReadOnlyList<string> MissingFields, string Source, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static FoodItemDto From(FoodItem f) => new(f.Id, f.HouseholdId, f.Name, f.IsArchived,
        f.ReferenceUnit, NutritionDto.From(f.Nutrition), f.Fodmap, f.Nova, f.DigestiveNotes, f.HasImage,
        f.MissingFields, f.Source.ToString(), f.CreatedAt, f.UpdatedAt);
}

public sealed record FoodRequest(
    [property: Required, StringLength(500)] string Name,
    [property: StringLength(50)] string ReferenceUnit,
    NutritionDto? Nutrition, string? Fodmap = null, int? Nova = null,
    [property: StringLength(2000)] string? DigestiveNotes = null);
