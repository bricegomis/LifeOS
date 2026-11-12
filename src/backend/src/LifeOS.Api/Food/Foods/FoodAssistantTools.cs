using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Api.Food.Products;
using LifeOS.Api.Food.Reviews;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Food.Foods;

/// <summary>A food with its products and the household's reviews, for the assistant.</summary>
public sealed record AssistantFoodSheet(FoodItemDto Food, List<ProductDto> Products, List<ReviewDto> Reviews);

/// <summary>Food tools for the assistant (ADR 0011): same code as the endpoints, through <see cref="FoodLibrary"/>.</summary>
[McpServerToolType]
public sealed class FoodAssistantTools
{
    private const string NutritionHelp =
        "Valeurs pour la base nutritionnelle (referenceUnit) : « 100 g », « 100 ml » ou « 1 pièce ». " +
        "Une valeur inconnue reste vide (null) ; 0 est une vraie valeur. Cherchez les valeurs dans une source fiable " +
        "(Ciqual en priorité) et citez-la à l'utilisateur. fodmap : low, moderate ou high. nova : 1 à 4.";

    [McpServerTool(Name = "search_foods", ReadOnly = true, Title = "Chercher des aliments")]
    [Description("Liste les aliments du foyer dont le nom contient le texte donné (tous si vide). " +
        "À appeler avant de créer un aliment pour éviter les doublons. missingFields liste ce qui reste à compléter.")]
    public static async Task<List<FoodItemDto>> SearchFoods(IHttpContextAccessor accessor, FoodLibrary foods,
        [Description("Texte cherché dans le nom, sans tenir compte de la casse.")] string? query = null,
        [Description("Inclure les aliments archivés.")] bool includeArchived = false,
        [Description("Nombre maximum de résultats.")] int limit = 50,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var found = await foods.ListAsync(household.HouseholdId, query, includeArchived, ct);
        return found.Take(Math.Clamp(limit, 1, 500)).Select(FoodItemDto.From).ToList();
    }

    [McpServerTool(Name = "get_food", ReadOnly = true, Title = "Fiche d'un aliment")]
    [Description("Renvoie un aliment avec ses produits (prix par magasin) et les avis des profils du foyer.")]
    public static async Task<AssistantFoodSheet> GetFood(IHttpContextAccessor accessor, FoodLibrary foods,
        ProductLibrary products, ReviewLibrary reviews, Guid foodId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var food = (await foods.FindAsync(household.HouseholdId, foodId, ct)).OrNotFound("Aliment");
        return new AssistantFoodSheet(FoodItemDto.From(food),
            (await products.ListAsync(household.HouseholdId, foodId, ct: ct)).Select(ProductDto.From).ToList(),
            await reviews.ListAsync(household.HouseholdId, ReviewTarget.Food(foodId), ct) ?? []);
    }

    [McpServerTool(Name = "create_food", Title = "Créer un aliment")]
    [Description("Crée un aliment générique (sans marque ni magasin : ce sont les produits). " + NutritionHelp +
        " Montrez d'abord à l'utilisateur ce qui va être créé.")]
    public static async Task<FoodItemDto> CreateFood(IHttpContextAccessor accessor, FoodLibrary foods,
        [Description("Nom générique, par exemple « Riz basmati ».")] string name,
        [Description("« 100 g », « 100 ml » ou « 1 pièce » ; vide si inconnue.")] string? referenceUnit = null,
        [Description("kcal par base.")] double? calories = null,
        [Description("Protéines en g par base.")] double? proteins = null,
        [Description("Glucides en g par base.")] double? carbs = null,
        [Description("Lipides en g par base.")] double? fats = null,
        [Description("Fibres en g par base.")] double? fibers = null,
        [Description("low, moderate ou high.")] string? fodmap = null,
        [Description("Groupe NOVA, de 1 à 4.")] int? nova = null,
        [Description("Notes digestives libres.")] string? digestiveNotes = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = Request(name, referenceUnit, calories, proteins, carbs, fats, fibers, fodmap, nova, digestiveNotes);
        return FoodItemDto.From(await foods.CreateAsync(household.HouseholdId, request, ct));
    }

    [McpServerTool(Name = "update_food", Idempotent = true, Title = "Modifier un aliment")]
    [Description("Remplace toute la fiche d'un aliment : un champ omis devient vide. Lisez-la d'abord avec get_food " +
        "et renvoyez toutes les valeurs à garder. " + NutritionHelp)]
    public static async Task<FoodItemDto> UpdateFood(IHttpContextAccessor accessor, FoodLibrary foods,
        Guid foodId, string name,
        string? referenceUnit = null, double? calories = null, double? proteins = null, double? carbs = null,
        double? fats = null, double? fibers = null, string? fodmap = null, int? nova = null, string? digestiveNotes = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = Request(name, referenceUnit, calories, proteins, carbs, fats, fibers, fodmap, nova, digestiveNotes);
        return FoodItemDto.From((await foods.UpdateAsync(household.HouseholdId, foodId, request, ct)).OrNotFound("Aliment"));
    }

    [McpServerTool(Name = "archive_food", Idempotent = true, Title = "Archiver un aliment")]
    [Description("Archive un aliment : il reste dans les recettes existantes mais ne peut plus être choisi. " +
        "Demandez confirmation à l'utilisateur avant.")]
    public static async Task<FoodItemDto> ArchiveFood(IHttpContextAccessor accessor, FoodLibrary foods, Guid foodId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return FoodItemDto.From((await foods.ArchiveAsync(household.HouseholdId, foodId, ct)).OrNotFound("Aliment"));
    }

    [McpServerTool(Name = "set_food_image_from_url", Idempotent = true, Title = "Image d'un aliment")]
    [Description("Télécharge une image (JPEG, PNG ou WebP, 10 Mo maximum, URL https publique) et la met sur l'aliment, " +
        "à la place de l'image actuelle. Préférez une photo simple de l'aliment brut, libre de droits si possible.")]
    public static async Task<FoodItemDto> SetFoodImageFromUrl(IHttpContextAccessor accessor, FoodLibrary foods,
        Guid foodId, [Description("Adresse https de l'image elle-même (pas de la page qui la contient).")] string url,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return FoodItemDto.From((await foods.ReplaceImageFromUrlAsync(household.HouseholdId, foodId, url, ct)).OrNotFound("Aliment"));
    }

    [McpServerTool(Name = "remove_food_image", Idempotent = true, Title = "Retirer l'image d'un aliment")]
    [Description("Retire l'image d'un aliment.")]
    public static async Task<FoodItemDto> RemoveFoodImage(IHttpContextAccessor accessor, FoodLibrary foods, Guid foodId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return FoodItemDto.From((await foods.RemoveImageAsync(household.HouseholdId, foodId, ct)).OrNotFound("Aliment"));
    }

    private static FoodRequest Request(string name, string? referenceUnit, double? calories, double? proteins, double? carbs,
        double? fats, double? fibers, string? fodmap, int? nova, string? digestiveNotes) =>
        AssistantRequest.Validated(new FoodRequest(name, referenceUnit ?? "",
            new NutritionDto
            {
                CaloriesPerUnit = calories, ProteinsPerUnit = proteins, CarbsPerUnit = carbs,
                FatsPerUnit = fats, FibersPerUnit = fibers,
            },
            string.IsNullOrWhiteSpace(fodmap) ? null : fodmap.Trim().ToLowerInvariant(), nova, digestiveNotes));
}
