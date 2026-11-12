using System.ComponentModel.DataAnnotations;
using LifeOS.Domain.Food.Recipes;

namespace LifeOS.Api.Food.Recipes;

public sealed record RecipeDto(
    Guid Id,
    string Name,
    int Servings,
    int DurationMinutes,
    List<string> Tags,
    List<RecipeIngredientDto> Ingredients,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsArchived,
    string? Instructions,
    bool HasImage)
{
    public static RecipeDto From(Recipe recipe) => new(
        recipe.Id, recipe.Name, recipe.Servings, recipe.DurationMinutes, recipe.Tags,
        recipe.Ingredients.Select(i => new RecipeIngredientDto(i.Id, i.FoodItemId, i.Quantity, i.Unit)).ToList(),
        recipe.CreatedAt, recipe.UpdatedAt, recipe.IsArchived, recipe.Instructions, recipe.HasImage);
}

public sealed record RecipeIngredientDto(Guid Id, Guid FoodItemId, decimal Quantity, string Unit);

public sealed record CreateRecipeRequest(
    [property: Required, StringLength(Recipe.MaxNameLength)] string Name,
    [property: Range(1, int.MaxValue)] int Servings,
    [property: Range(0, int.MaxValue)] int DurationMinutes,
    List<string>? Tags = null,
    [property: StringLength(Recipe.MaxInstructionsLength)] string? Instructions = null);

/// <summary>Partial update: an omitted field is left unchanged.</summary>
public sealed record UpdateRecipeRequest(
    [property: StringLength(Recipe.MaxNameLength, MinimumLength = 1)] string? Name = null,
    [property: Range(1, int.MaxValue)] int? Servings = null,
    [property: Range(0, int.MaxValue)] int? DurationMinutes = null,
    List<string>? Tags = null,
    [property: StringLength(Recipe.MaxInstructionsLength)] string? Instructions = null);

public sealed record AddRecipeIngredientRequest(
    Guid FoodItemId,
    decimal Quantity,
    [property: Required, StringLength(RecipeIngredient.MaxUnitLength)] string Unit);

public sealed record UpdateRecipeIngredientRequest(
    decimal Quantity,
    [property: Required, StringLength(RecipeIngredient.MaxUnitLength)] string Unit);
