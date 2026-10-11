namespace LifeOS.Api.Dtos;

public record ManualWeekRequest(DateOnly StartsOn, string TimeZoneId = "Europe/Paris");
public record ManualWeekSummary(Guid Id, DateOnly StartsOn, string TimeZoneId, bool IsManual);
public record ManualWeekDto(Guid Id, DateOnly StartsOn, string TimeZoneId, bool IsManual, List<ManualDayDto> Days);
public record ManualDayDto(Guid Id, DateOnly Date, List<ManualMealDto> Meals, List<ManualSportDto> Sports, PersonalNutritionDto Nutrition);
public record PersonalNutritionDto(double? Calories, double? Protein, double? Carbs, double? Fat, bool IsComplete, List<string> Warnings);
public record PlannedLineIngredientDto(Guid Id, Guid FoodItemId, string Name, decimal Quantity, string Unit,
    decimal RequiredQuantity, string ReferenceUnit, double? Calories, double? Protein, double? Carbs, double? Fat);
public record PlannedLinePortionDto(Guid MemberProfileId, string MemberName, decimal Servings);
public record PlannedRecipeLineDto(Guid Id, Guid? RecipeId, string? Name, string? Instructions,
    decimal YieldServings, decimal PlannedServings, bool IsAdHoc,
    List<PlannedLinePortionDto> Portions, List<PlannedLineIngredientDto> Ingredients,
    PersonalNutritionDto Nutrition);
public record ManualMealDto(Guid Id, Guid DayPlanId, string Name, int? StartMinute, int? EndMinute,
    List<PlannedRecipeLineDto> RecipeLines, PersonalNutritionDto Nutrition);
public record ManualSportDto(Guid Id, Guid DayPlanId, Guid? SportTemplateId, string Name,
    string Sport, string Intensity, int DurationMinutes, decimal? DistanceKm, decimal Calories,
    int? StartMinute, int? EndMinute);
public record PlannedLineIngredientRequest(Guid FoodItemId, decimal Quantity, string Unit);
public record PlannedLinePortionRequest(Guid MemberProfileId, decimal Servings);
public record PlannedRecipeLineRequest(Guid? RecipeId, string? Name, string? Instructions,
    decimal? YieldServings, decimal? PlannedServings, List<PlannedLineIngredientRequest>? Ingredients = null,
    List<PlannedLinePortionRequest>? Portions = null);
public record ManualMealRequest(Guid DayPlanId, int? StartMinute, int? EndMinute,
    List<PlannedRecipeLineRequest> RecipeLines, bool ReplaceContent = false);
public record ManualSportRequest(Guid DayPlanId, int? StartMinute, int? EndMinute, Guid? SportTemplateId,
    string Name, string Sport, string Intensity, int DurationMinutes, decimal? DistanceKm, decimal Calories,
    bool ReplaceContent = false);
