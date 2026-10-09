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
            ArticleId = foodItem.ArticleId,
            IsArchived = foodItem.IsArchived,
            Name = foodItem.Name,
            Description = foodItem.Description,
            Unit = LifeOS.Application.Articles.ArticleMapper.ToUnitString(foodItem.Unit),
            PurchaseUnitConfirmed = foodItem.PurchaseUnitConfirmed,
            LegacyPurchaseName = foodItem.LegacyPurchaseName,
            MigrationOrigin = foodItem.MigrationOrigin,
            PriceHistory = foodItem.PriceHistory.Select(e => new LifeOS.Application.Articles.GroceryPriceEntryDto(
                e.Id, e.StoreId, e.Price, e.ObservedAt, e.CreatedAt)).OrderByDescending(e => e.ObservedAt).ToList(),
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
