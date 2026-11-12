using LifeOS.Api.Food.Files;
using LifeOS.Api.Food.Foods;
using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Recipes;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Recipes;

/// <summary>
/// Recipe management shared by the endpoints and the assistant tools (ADR 0011): plain DbContext
/// access on the Food tables, rules in <see cref="Recipe"/>. Every method is scoped to one
/// household; a null result means "not in this household".
/// </summary>
public sealed class RecipeLibrary(LifeOSDbContext db, FoodImageStorage images, IRemoteImageSource remoteImages,
    ILogger<RecipeLibrary> logger)
{
    public async Task<List<Recipe>> ListAsync(Guid householdId, string? search = null, bool includeArchived = true,
        CancellationToken ct = default)
    {
        var query = db.Recipes.AsNoTracking().Include(recipe => recipe.Ingredients).Where(recipe => recipe.HouseholdId == householdId);
        if (!includeArchived) query = query.Where(recipe => !recipe.IsArchived);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(recipe => EF.Functions.ILike(recipe.Name, $"%{FoodLibrary.EscapeLike(search.Trim())}%", "\\"));
        return await query.OrderBy(recipe => recipe.Name).ThenBy(recipe => recipe.Id).ToListAsync(ct);
    }

    public Task<Recipe?> FindAsync(Guid householdId, Guid recipeId, CancellationToken ct) =>
        db.Recipes.Include(recipe => recipe.Ingredients)
            .SingleOrDefaultAsync(recipe => recipe.Id == recipeId && recipe.HouseholdId == householdId, ct);

    /// <summary>The household's foods used by these recipes, archived ones included, by id.</summary>
    public async Task<Dictionary<Guid, FoodItem>> FoodsOfAsync(Guid householdId, IEnumerable<Recipe> recipes, CancellationToken ct)
    {
        var ids = recipes.SelectMany(recipe => recipe.Ingredients).Select(ingredient => ingredient.FoodItemId).Distinct().ToList();
        return await db.FoodItems.AsNoTracking().Where(food => food.HouseholdId == householdId && ids.Contains(food.Id))
            .ToDictionaryAsync(food => food.Id, ct);
    }

    /// <summary>Creates the recipe and its ingredients in one save: nothing is kept if one line is invalid.</summary>
    public async Task<Recipe> CreateAsync(Guid householdId, CreateRecipeRequest request,
        IReadOnlyList<AddRecipeIngredientRequest>? ingredients, CancellationToken ct)
    {
        var recipe = Recipe.Create(householdId, request.Name, request.Servings, request.DurationMinutes,
            request.Tags, request.Instructions);
        db.Recipes.Add(recipe);
        foreach (var line in ingredients ?? [])
        {
            await RequireActiveFood(householdId, line.FoodItemId, ct);
            recipe.AddIngredient(line.FoodItemId, line.Quantity, line.Unit);
        }
        await db.SaveChangesAsync(ct);
        return recipe;
    }

    public async Task<Recipe?> UpdateAsync(Guid householdId, Guid recipeId, UpdateRecipeRequest request, CancellationToken ct)
    {
        var recipe = await FindAsync(householdId, recipeId, ct);
        if (recipe is null) return null;
        recipe.Update(request.Name, request.Servings, request.DurationMinutes, request.Tags, request.Instructions);
        await db.SaveChangesAsync(ct);
        return recipe;
    }

    public async Task<Recipe?> ArchiveAsync(Guid householdId, Guid recipeId, CancellationToken ct)
    {
        var recipe = await FindAsync(householdId, recipeId, ct);
        if (recipe is null) return null;
        recipe.Archive();
        await db.SaveChangesAsync(ct);
        return recipe;
    }

    public async Task<Recipe?> AddIngredientAsync(Guid householdId, Guid recipeId, AddRecipeIngredientRequest request, CancellationToken ct)
    {
        var recipe = await FindAsync(householdId, recipeId, ct);
        if (recipe is null) return null;
        await RequireActiveFood(householdId, request.FoodItemId, ct);
        // Added through the DbSet so EF inserts the line instead of guessing its state from the key.
        db.RecipeIngredients.Add(recipe.AddIngredient(request.FoodItemId, request.Quantity, request.Unit));
        await db.SaveChangesAsync(ct);
        return recipe;
    }

    /// <returns>Null when the recipe or the ingredient is not found.</returns>
    public async Task<Recipe?> UpdateIngredientAsync(Guid householdId, Guid recipeId, Guid ingredientId,
        UpdateRecipeIngredientRequest request, CancellationToken ct)
    {
        var recipe = await FindAsync(householdId, recipeId, ct);
        if (recipe is null || !recipe.UpdateIngredient(ingredientId, request.Quantity, request.Unit)) return null;
        await db.SaveChangesAsync(ct);
        return recipe;
    }

    /// <returns>Null when the recipe or the ingredient is not found.</returns>
    public async Task<Recipe?> RemoveIngredientAsync(Guid householdId, Guid recipeId, Guid ingredientId, CancellationToken ct)
    {
        var recipe = await FindAsync(householdId, recipeId, ct);
        if (recipe is null || !recipe.RemoveIngredient(ingredientId)) return null;
        await db.SaveChangesAsync(ct);
        return recipe;
    }

    public async Task<Recipe?> ReplaceImageAsync(Guid householdId, Guid recipeId, Stream content, long length, string? contentType,
        CancellationToken ct)
    {
        var recipe = await FindAsync(householdId, recipeId, ct);
        if (recipe is null) return null;
        await images.ReplaceImageAsync(content, length, contentType, recipe.ImagePath,
            stored => recipe.SetImage(stored.FileName, stored.ContentType), () => db.SaveChangesAsync(ct), logger, ct);
        return recipe;
    }

    /// <summary>Downloads an image found by the assistant, then stores it like an upload.</summary>
    public async Task<Recipe?> ReplaceImageFromUrlAsync(Guid householdId, Guid recipeId, string url, CancellationToken ct)
    {
        if (await FindAsync(householdId, recipeId, ct) is null) return null;
        var image = await remoteImages.DownloadAsync(url, ct);
        using var content = new MemoryStream(image.Bytes);
        return await ReplaceImageAsync(householdId, recipeId, content, image.Bytes.Length, image.ContentType, ct);
    }

    public async Task<Recipe?> RemoveImageAsync(Guid householdId, Guid recipeId, CancellationToken ct)
    {
        var recipe = await FindAsync(householdId, recipeId, ct);
        if (recipe is null) return null;
        var oldPath = recipe.ImagePath;
        recipe.RemoveImage();
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove a deleted recipe image"));
        return recipe;
    }

    // Foods belong to the same module; the household filter rejects cross-household references.
    private async Task RequireActiveFood(Guid householdId, Guid foodId, CancellationToken ct)
    {
        if (!await db.FoodItems.AnyAsync(food => food.Id == foodId && food.HouseholdId == householdId && !food.IsArchived, ct))
            throw new ArgumentException("L'ingrédient doit référencer un aliment actif de votre foyer.");
    }
}
