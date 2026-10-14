using LifeOS.Domain.FoodItems;

namespace LifeOS.Application.FoodItems;

public static class FoodItemMapper
{
    public static FoodItemDto ToDto(FoodItem f) => new(f.Id, f.HouseholdId, f.Name, f.IsArchived,
        f.ReferenceUnit, f.Nutrition is null ? null : new NutritionDto
        {
            CaloriesPerUnit = f.Nutrition.CaloriesPerUnit, ProteinsPerUnit = f.Nutrition.ProteinsPerUnit,
            CarbsPerUnit = f.Nutrition.CarbsPerUnit, FatsPerUnit = f.Nutrition.FatsPerUnit,
        }, f.Fodmap, f.Nova, f.DigestiveNotes, f.HasImage, f.MissingFields, f.Source.ToString(), f.CreatedAt, f.UpdatedAt);

    public static Nutrition? NutritionFromDto(NutritionDto? dto) => dto is null ? null
        : new Nutrition(dto.CaloriesPerUnit, dto.ProteinsPerUnit, dto.CarbsPerUnit, dto.FatsPerUnit);
}
