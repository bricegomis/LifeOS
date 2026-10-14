using System.ComponentModel.DataAnnotations;
using LifeOS.Application.FoodItems;

namespace LifeOS.Api.Contracts;

public sealed record FoodRequest(
    [property: Required, StringLength(500)] string Name,
    [property: StringLength(50)] string ReferenceUnit,
    NutritionDto? Nutrition, string? Fodmap = null, int? Nova = null,
    [property: StringLength(2000)] string? DigestiveNotes = null);
