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

    public string Name { get; set; }
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

    public string Name { get; set; }
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

    public string Name { get; set; }
    public string ReferenceUnit { get; set; }
    public NutritionDto? Nutrition { get; set; }
}
