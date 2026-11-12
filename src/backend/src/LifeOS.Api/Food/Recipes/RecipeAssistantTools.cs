using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Api.Food.Reviews;
using LifeOS.Application.Food.Costs;
using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Recipes;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Food.Recipes;

public sealed record AssistantRecipeSummary(Guid Id, string Name, int Servings, int DurationMinutes, List<string> Tags,
    int IngredientCount, bool HasImage, bool IsArchived);

public sealed record AssistantIngredient(Guid Id, Guid FoodId, string FoodName, decimal Quantity, string Unit);

public sealed record AssistantIngredientInput(
    [property: Description("Identifiant d'un aliment actif du foyer (search_foods).")] Guid FoodId,
    [property: Description("Quantité pour toute la recette (le rendement), pas par portion.")] decimal Quantity,
    [property: Description("g, ml ou pièce.")] string Unit);

/// <summary>Per serving; <c>IncompleteFoods</c> names the foods whose nutrition is missing or in another unit.</summary>
public sealed record AssistantRecipeNutrition(List<NutrientPerServing> PerServing, List<string> IncompleteFoods, bool IsComplete);

/// <summary>A recipe with its ingredients, nutrition and min–max cost, for the assistant.</summary>
public sealed record AssistantRecipe(Guid Id, string Name, int Servings, int DurationMinutes, List<string> Tags,
    string? Instructions, bool HasImage, bool IsArchived, List<AssistantIngredient> Ingredients,
    AssistantRecipeNutrition Nutrition, RecipeCostDto? Cost, List<ReviewDto> Reviews);

/// <summary>Recipe tools for the assistant (ADR 0011): same code as the endpoints, through <see cref="RecipeLibrary"/>.</summary>
[McpServerToolType]
public sealed class RecipeAssistantTools
{
    [McpServerTool(Name = "search_recipes", ReadOnly = true, Title = "Chercher des recettes")]
    [Description("Liste les recettes du foyer dont le nom contient le texte donné (toutes si vide), éventuellement " +
        "filtrées par catégorie. À appeler avant de créer une recette pour éviter les doublons.")]
    public static async Task<List<AssistantRecipeSummary>> SearchRecipes(IHttpContextAccessor accessor, RecipeLibrary recipes,
        [Description("Texte cherché dans le nom, sans tenir compte de la casse.")] string? query = null,
        [Description("Catégorie exacte (par exemple « plat »).")] string? tag = null,
        [Description("Inclure les recettes archivées.")] bool includeArchived = false,
        [Description("Nombre maximum de résultats.")] int limit = 50,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var found = await recipes.ListAsync(household.HouseholdId, query, includeArchived, ct);
        return found
            .Where(r => string.IsNullOrWhiteSpace(tag) || r.Tags.Contains(tag.Trim(), StringComparer.OrdinalIgnoreCase))
            .Take(Math.Clamp(limit, 1, 500))
            .Select(r => new AssistantRecipeSummary(r.Id, r.Name, r.Servings, r.DurationMinutes, r.Tags, r.Ingredients.Count, r.HasImage, r.IsArchived))
            .ToList();
    }

    [McpServerTool(Name = "get_recipe", ReadOnly = true, Title = "Fiche d'une recette")]
    [Description("Renvoie une recette complète : ingrédients avec le nom des aliments, instructions, nutrition par portion " +
        "(partielle tant qu'un aliment n'est pas complet), coût minimum et maximum tous magasins confondus, et avis du foyer.")]
    public static async Task<AssistantRecipe> GetRecipe(IHttpContextAccessor accessor, RecipeLibrary recipes,
        RecipeCosts costs, ReviewLibrary reviews, Guid recipeId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var recipe = (await recipes.FindAsync(household.HouseholdId, recipeId, ct)).OrNotFound("Recette");
        return await Sheet(household.HouseholdId, recipe, recipes, costs, reviews, ct);
    }

    [McpServerTool(Name = "create_recipe", Title = "Créer une recette")]
    [Description("Crée une recette avec ses ingrédients en une fois ; rien n'est créé si un ingrédient est invalide. " +
        "Les ingrédients sont des aliments du foyer (créez d'abord ceux qui manquent avec create_food), jamais des produits. " +
        "Les quantités sont pour tout le rendement (servings). Montrez d'abord la recette à l'utilisateur.")]
    public static async Task<AssistantRecipe> CreateRecipe(IHttpContextAccessor accessor, RecipeLibrary recipes,
        RecipeCosts costs, ReviewLibrary reviews,
        string name,
        [Description("Rendement en portions (au moins 1).")] int servings,
        [Description("Durée totale en minutes.")] int durationMinutes = 0,
        [Description("Catégories, par exemple « plat », « dessert ».")] List<string>? tags = null,
        [Description("Étapes de préparation, texte libre.")] string? instructions = null,
        List<AssistantIngredientInput>? ingredients = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new CreateRecipeRequest(name, servings, durationMinutes, tags, instructions));
        var lines = (ingredients ?? []).Select(i => AssistantRequest.Validated(new AddRecipeIngredientRequest(i.FoodId, i.Quantity, i.Unit))).ToList();
        var recipe = await recipes.CreateAsync(household.HouseholdId, request, lines, ct);
        return await Sheet(household.HouseholdId, recipe, recipes, costs, reviews, ct);
    }

    [McpServerTool(Name = "update_recipe", Idempotent = true, Title = "Modifier une recette")]
    [Description("Modifie les informations d'une recette ; un champ omis reste inchangé. tags remplace toute la liste. " +
        "Pour les ingrédients, utilisez add_recipe_ingredient, update_recipe_ingredient et remove_recipe_ingredient.")]
    public static async Task<AssistantRecipe> UpdateRecipe(IHttpContextAccessor accessor, RecipeLibrary recipes,
        RecipeCosts costs, ReviewLibrary reviews, Guid recipeId,
        string? name = null, int? servings = null, int? durationMinutes = null, List<string>? tags = null,
        [Description("Nouveau texte complet des instructions.")] string? instructions = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new UpdateRecipeRequest(name, servings, durationMinutes, tags, instructions));
        var recipe = (await recipes.UpdateAsync(household.HouseholdId, recipeId, request, ct)).OrNotFound("Recette");
        return await Sheet(household.HouseholdId, recipe, recipes, costs, reviews, ct);
    }

    [McpServerTool(Name = "add_recipe_ingredient", Title = "Ajouter un ingrédient")]
    [Description("Ajoute un aliment du foyer à une recette, quantité pour tout le rendement, en g, ml ou pièce.")]
    public static async Task<AssistantRecipe> AddRecipeIngredient(IHttpContextAccessor accessor, RecipeLibrary recipes,
        RecipeCosts costs, ReviewLibrary reviews, Guid recipeId, Guid foodId, decimal quantity,
        [Description("g, ml ou pièce.")] string unit, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new AddRecipeIngredientRequest(foodId, quantity, unit));
        var recipe = (await recipes.AddIngredientAsync(household.HouseholdId, recipeId, request, ct)).OrNotFound("Recette");
        return await Sheet(household.HouseholdId, recipe, recipes, costs, reviews, ct);
    }

    [McpServerTool(Name = "update_recipe_ingredient", Idempotent = true, Title = "Modifier un ingrédient")]
    [Description("Change la quantité et l'unité d'une ligne d'ingrédient (identifiant de ligne donné par get_recipe).")]
    public static async Task<AssistantRecipe> UpdateRecipeIngredient(IHttpContextAccessor accessor, RecipeLibrary recipes,
        RecipeCosts costs, ReviewLibrary reviews, Guid recipeId, Guid ingredientId, decimal quantity,
        [Description("g, ml ou pièce.")] string unit, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new UpdateRecipeIngredientRequest(quantity, unit));
        var recipe = (await recipes.UpdateIngredientAsync(household.HouseholdId, recipeId, ingredientId, request, ct))
            .OrNotFound("Recette ou ingrédient");
        return await Sheet(household.HouseholdId, recipe, recipes, costs, reviews, ct);
    }

    [McpServerTool(Name = "remove_recipe_ingredient", Title = "Retirer un ingrédient")]
    [Description("Retire une ligne d'ingrédient d'une recette (identifiant de ligne donné par get_recipe).")]
    public static async Task<AssistantRecipe> RemoveRecipeIngredient(IHttpContextAccessor accessor, RecipeLibrary recipes,
        RecipeCosts costs, ReviewLibrary reviews, Guid recipeId, Guid ingredientId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var recipe = (await recipes.RemoveIngredientAsync(household.HouseholdId, recipeId, ingredientId, ct))
            .OrNotFound("Recette ou ingrédient");
        return await Sheet(household.HouseholdId, recipe, recipes, costs, reviews, ct);
    }

    [McpServerTool(Name = "archive_recipe", Idempotent = true, Title = "Archiver une recette")]
    [Description("Archive une recette : elle disparaît de la bibliothèque active. Demandez confirmation à l'utilisateur avant.")]
    public static async Task<AssistantRecipeSummary> ArchiveRecipe(IHttpContextAccessor accessor, RecipeLibrary recipes,
        Guid recipeId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var r = (await recipes.ArchiveAsync(household.HouseholdId, recipeId, ct)).OrNotFound("Recette");
        return new AssistantRecipeSummary(r.Id, r.Name, r.Servings, r.DurationMinutes, r.Tags, r.Ingredients.Count, r.HasImage, r.IsArchived);
    }

    [McpServerTool(Name = "set_recipe_image_from_url", Idempotent = true, Title = "Image d'une recette")]
    [Description("Télécharge une image (JPEG, PNG ou WebP, 10 Mo maximum, URL https publique) et la met sur la recette, " +
        "à la place de l'image actuelle.")]
    public static async Task<AssistantRecipeSummary> SetRecipeImageFromUrl(IHttpContextAccessor accessor, RecipeLibrary recipes,
        Guid recipeId, [Description("Adresse https de l'image elle-même (pas de la page qui la contient).")] string url,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var r = (await recipes.ReplaceImageFromUrlAsync(household.HouseholdId, recipeId, url, ct)).OrNotFound("Recette");
        return new AssistantRecipeSummary(r.Id, r.Name, r.Servings, r.DurationMinutes, r.Tags, r.Ingredients.Count, r.HasImage, r.IsArchived);
    }

    [McpServerTool(Name = "remove_recipe_image", Idempotent = true, Title = "Retirer l'image d'une recette")]
    [Description("Retire l'image d'une recette.")]
    public static async Task<AssistantRecipeSummary> RemoveRecipeImage(IHttpContextAccessor accessor, RecipeLibrary recipes,
        Guid recipeId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var r = (await recipes.RemoveImageAsync(household.HouseholdId, recipeId, ct)).OrNotFound("Recette");
        return new AssistantRecipeSummary(r.Id, r.Name, r.Servings, r.DurationMinutes, r.Tags, r.Ingredients.Count, r.HasImage, r.IsArchived);
    }

    private static async Task<AssistantRecipe> Sheet(Guid householdId, Recipe recipe, RecipeLibrary recipes, RecipeCosts costs,
        ReviewLibrary reviews, CancellationToken ct)
    {
        var foods = await recipes.FoodsOfAsync(householdId, [recipe], ct);
        string NameOf(Guid foodId) => foods.GetValueOrDefault(foodId)?.Name ?? "Aliment indisponible";
        var nutrition = RecipeNutritionCalculator.Compute(recipe, foods);
        var cost = await costs.OfRecipeAsync(householdId, recipe.Id, ct);
        return new AssistantRecipe(recipe.Id, recipe.Name, recipe.Servings, recipe.DurationMinutes, recipe.Tags,
            recipe.Instructions, recipe.HasImage, recipe.IsArchived,
            recipe.Ingredients.Select(i => new AssistantIngredient(i.Id, i.FoodItemId, NameOf(i.FoodItemId), i.Quantity, i.Unit)).ToList(),
            new AssistantRecipeNutrition(
                nutrition.PerServing.Select(n => n with { Value = Math.Round(n.Value, 1) }).ToList(),
                nutrition.IncompleteFoodIds.Select(NameOf).ToList(), nutrition.IsComplete),
            cost is null ? null : RecipeCostDto.From(cost),
            await reviews.ListAsync(householdId, ReviewTarget.Recipe(recipe.Id), ct) ?? []);
    }
}
