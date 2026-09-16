using System.ComponentModel.DataAnnotations;
using LifeOS.Application.FoodItems;

namespace LifeOS.Api.Contracts;

/// <summary>
/// Request DTO for creating a food item (Jalon 3).
/// </summary>
public sealed class CreateFoodItemRequest
{
    public CreateFoodItemRequest(string name, string referenceUnit, NutritionDto? nutrition)
    {
        Name = name;
        ReferenceUnit = referenceUnit;
        Nutrition = nutrition;
    }

    [Required]
    [StringLength(500)]
    public string Name { get; set; }

    [Required]
    [StringLength(50)]
    public string ReferenceUnit { get; set; }

    public NutritionDto? Nutrition { get; set; }
}

/// <summary>
/// Request DTO for updating a food item (Jalon 3).
/// </summary>
public sealed class UpdateFoodItemRequest
{
    public UpdateFoodItemRequest(string name, string referenceUnit, NutritionDto? nutrition)
    {
        Name = name;
        ReferenceUnit = referenceUnit;
        Nutrition = nutrition;
    }

    [Required]
    [StringLength(500)]
    public string Name { get; set; }

    [Required]
    [StringLength(50)]
    public string ReferenceUnit { get; set; }

    public NutritionDto? Nutrition { get; set; }
}

/// <summary>
/// Request DTO for creating a correction for a food item (Jalon 3).
/// </summary>
public sealed class CreateCorrectionRequest
{
    public CreateCorrectionRequest(string name, string referenceUnit, NutritionDto? nutrition)
    {
        Name = name;
        ReferenceUnit = referenceUnit;
        Nutrition = nutrition;
    }

    [Required]
    [StringLength(500)]
    public string Name { get; set; }

    [Required]
    [StringLength(50)]
    public string ReferenceUnit { get; set; }

    public NutritionDto? Nutrition { get; set; }
}
