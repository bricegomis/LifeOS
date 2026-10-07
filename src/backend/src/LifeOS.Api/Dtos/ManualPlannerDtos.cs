namespace LifeOS.Api.Dtos;

public record ManualWeekRequest(DateOnly StartsOn, string TimeZoneId = "Europe/Paris");
public record ManualWeekSummary(Guid Id, DateOnly StartsOn, string TimeZoneId, bool IsManual);
public record ManualWeekDto(Guid Id, DateOnly StartsOn, string TimeZoneId, bool IsManual, List<ManualDayDto> Days);
public record ManualDayDto(Guid Id, DateOnly Date, List<ManualMealDto> Meals, List<ManualSportDto> Sports, PersonalNutritionDto Nutrition);
public record PersonalNutritionDto(double? Calories, double? Protein, double? Carbs, double? Fat, bool IsComplete, List<string> Warnings);
public record MealLineDto(Guid Id, Guid? FoodItemId, string Name, decimal Quantity, string Unit,
    decimal PersonalQuantity, decimal PreparationQuantity, string ReferenceUnit,
    double? Calories, double? Protein, double? Carbs, double? Fat);
public record ManualMealDto(Guid Id, Guid DayPlanId, string Name, int? StartMinute, int? EndMinute,
    Guid? RecipeId, Guid? ComposedMealId, decimal PersonalPortion, int ChildrenCount,
    bool HasSnapshot, List<MealLineDto> Lines, PersonalNutritionDto Nutrition);
public record ManualSportDto(Guid Id, Guid DayPlanId, Guid? SportTemplateId, string Name,
    string Sport, string Intensity, int DurationMinutes, decimal? DistanceKm, decimal Calories,
    int? StartMinute, int? EndMinute);
public record MealLineRequest(Guid? FoodItemId, decimal Quantity, string Unit, Guid? SnapshotLineId = null);
public record ManualMealRequest(Guid DayPlanId, int? StartMinute, int? EndMinute, decimal PersonalPortion,
    int ChildrenCount, Guid? RecipeId = null, List<MealLineRequest>? Lines = null,
    bool ReplaceContent = false, string? Name = null);
public record ManualSportRequest(Guid DayPlanId, int? StartMinute, int? EndMinute, Guid? SportTemplateId,
    string Name, string Sport, string Intensity, int DurationMinutes, decimal? DistanceKm, decimal Calories,
    bool ReplaceContent = false);
