using LifeOS.Domain.FoodItems;

namespace LifeOS.Application.FoodItems;

/// <summary>
/// Mapper for converting FoodItem domain entities to DTOs and vice versa.
/// </summary>
public static class FoodItemMapper
{
    public static FoodItemDto ToDto(FoodItem foodItem)
    {
        return new FoodItemDto
        {
            Id = foodItem.Id,
            HouseholdId = foodItem.HouseholdId,
            Name = foodItem.Name,
            ReferenceUnit = foodItem.ReferenceUnit,
            Nutrition = foodItem.Nutrition == null ? null : new NutritionDto
            {
                CaloriesPerUnit = foodItem.Nutrition.CaloriesPerUnit,
                ProteinsPerUnit = foodItem.Nutrition.ProteinsPerUnit,
                CarbsPerUnit = foodItem.Nutrition.CarbsPerUnit,
                FatsPerUnit = foodItem.Nutrition.FatsPerUnit,
            },
            Source = foodItem.Source.ToString(),
            OffBarcode = foodItem.OffBarcode,
            IsCorrectionOf = foodItem.IsCorrectionOf,
            CreatedAt = foodItem.CreatedAt,
            UpdatedAt = foodItem.UpdatedAt,
        };
    }

    public static Nutrition? NutritionFromDto(NutritionDto? dto)
    {
        if (dto == null)
            return null;

        return new Nutrition(dto.CaloriesPerUnit, dto.ProteinsPerUnit, dto.CarbsPerUnit, dto.FatsPerUnit);
    }
}
