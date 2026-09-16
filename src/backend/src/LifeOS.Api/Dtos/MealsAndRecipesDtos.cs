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
    string Name,
    int Servings,
    int DurationMinutes,
    List<string>? Tags = null,
    Dictionary<string, object>? Metadata = null);

public record UpdateRecipeRequest(
    string? Name = null,
    int? Servings = null,
    int? DurationMinutes = null,
    List<string>? Tags = null,
    Dictionary<string, object>? Metadata = null);

public record AddRecipeIngredientRequest(
    Guid FoodItemId,
    decimal Quantity,
    string Unit);

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
    string Name);

public record UpdateComposedMealRequest(
    string? Name = null);

public record AddComposedMealPartRequest(
    Guid RecipeId,
    decimal QuantityFactor);

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
    string Status = "draft");

public record UpdateWeekStatusRequest(
    string Status);

public record CreateDayPlanRequest(
    DateOnly Date,
    string WorkContext = "home",
    bool BikeCommute = false);

public record UpdateDayPlanRequest(
    string? WorkContext = null,
    bool? BikeCommute = null);

public record CreatePlannedMealRequest(
    string MealType,
    Guid? ComposedMealId = null,
    Guid? RecipeId = null,
    string Status = "planned");

public record UpdatePlannedMealStatusRequest(
    string Status);

public record ReplacePlannedMealRequest(
    Guid? ComposedMealId = null,
    Guid? RecipeId = null);

public record AddPlannedMealPartRequest(
    Guid MemberProfileId,
    decimal PortionMultiplier);

public record UpdatePlannedMealPartRequest(
    decimal PortionMultiplier);
