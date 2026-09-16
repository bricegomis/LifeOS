using System.ComponentModel.DataAnnotations;

namespace LifeOS.Api.Dtos;

// Recipe DTOs
public record RecipeDto(
    Guid Id,
    string Name,
    int Servings,
    int DurationMinutes,
    List<string> Tags,
    Dictionary<string, object>? Metadata,
    List<RecipeIngredientDto> Ingredients,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record RecipeIngredientDto(
    Guid Id,
    Guid FoodItemId,
    decimal Quantity,
    string Unit);

public record CreateRecipeRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Range(1, int.MaxValue)] int Servings,
    [property: Range(0, int.MaxValue)] int DurationMinutes,
    List<string>? Tags = null,
    Dictionary<string, object>? Metadata = null);

public record UpdateRecipeRequest(
    [property: StringLength(200, MinimumLength = 1)] string? Name = null,
    [property: Range(1, int.MaxValue)] int? Servings = null,
    [property: Range(0, int.MaxValue)] int? DurationMinutes = null,
    List<string>? Tags = null,
    Dictionary<string, object>? Metadata = null);

public record AddRecipeIngredientRequest(
    Guid FoodItemId,
    [property: Range(double.Epsilon, double.MaxValue)] decimal Quantity,
    [property: Required, StringLength(50)] string Unit);

// ComposedMeal DTOs
public record ComposedMealDto(
    Guid Id,
    string Name,
    List<ComposedMealPartDto> Parts,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ComposedMealPartDto(
    Guid Id,
    Guid RecipeId,
    decimal QuantityFactor);

public record CreateComposedMealRequest(
    [property: Required, StringLength(200)] string Name);

public record UpdateComposedMealRequest(
    [property: StringLength(200, MinimumLength = 1)] string? Name = null);

public record AddComposedMealPartRequest(
    Guid RecipeId,
    [property: Range(double.Epsilon, double.MaxValue)] decimal QuantityFactor);

// Week Planning DTOs
public record WeekDto(
    Guid Id,
    DateOnly StartsOn,
    string Status,
    List<DayPlanDto> DayPlans,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record DayPlanDto(
    Guid Id,
    DateOnly Date,
    string WorkContext,
    bool BikeCommute,
    List<PlannedMealDto> PlannedMeals);

public record PlannedMealDto(
    Guid Id,
    string MealType,
    string Status,
    Guid? ComposedMealId,
    Guid? RecipeId,
    List<PlannedMealPartDto> Parts,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record PlannedMealPartDto(
    Guid Id,
    Guid MemberProfileId,
    decimal PortionMultiplier);

public record CreateWeekRequest(
    DateOnly StartsOn,
    [property: Required, StringLength(50)] string Status = "draft");

public record UpdateWeekStatusRequest(
    [property: Required, StringLength(50)] string Status);

public record CreateDayPlanRequest(
    DateOnly Date,
    [property: Required, StringLength(50)] string WorkContext = "home",
    bool BikeCommute = false);

public record UpdateDayPlanRequest(
    [property: StringLength(50, MinimumLength = 1)] string? WorkContext = null,
    bool? BikeCommute = null);

public record CreatePlannedMealRequest(
    [property: Required, StringLength(50)] string MealType,
    Guid? ComposedMealId = null,
    Guid? RecipeId = null,
    [property: Required, StringLength(50)] string Status = "planned");

public record UpdatePlannedMealStatusRequest(
    [property: Required, StringLength(50)] string Status);

public record ReplacePlannedMealRequest(
    Guid? ComposedMealId = null,
    Guid? RecipeId = null);

public record AddPlannedMealPartRequest(
    Guid MemberProfileId,
    [property: Range(double.Epsilon, double.MaxValue)] decimal PortionMultiplier);

public record UpdatePlannedMealPartRequest(
    [property: Range(double.Epsilon, double.MaxValue)] decimal PortionMultiplier);
