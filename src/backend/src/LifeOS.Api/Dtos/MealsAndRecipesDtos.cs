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
    DateTimeOffset UpdatedAt,
    bool IsArchived = false,
    string? Instructions = null);

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
    Dictionary<string, object>? Metadata = null,
    [property: StringLength(10000)] string? Instructions = null);

public record UpdateRecipeRequest(
    [property: StringLength(200, MinimumLength = 1)] string? Name = null,
    [property: Range(1, int.MaxValue)] int? Servings = null,
    [property: Range(0, int.MaxValue)] int? DurationMinutes = null,
    List<string>? Tags = null,
    Dictionary<string, object>? Metadata = null,
    [property: StringLength(10000)] string? Instructions = null);

public record AddRecipeIngredientRequest(
    Guid FoodItemId,
    [property: Range(double.Epsilon, double.MaxValue)] decimal Quantity,
    [property: Required, StringLength(50)] string Unit);
public record UpdateRecipeIngredientRequest(
    [property: Range(double.Epsilon, double.MaxValue)] decimal Quantity,
    [property: Required, StringLength(50)] string Unit);
