using LifeOS.Api.Food.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Food.Recipes;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Recipes;

/// <summary>
/// Simple management screen (ADR 0009): the endpoints read and write the Food module's own
/// tables directly; validity rules live in <see cref="Recipe"/>. Invalid input raised by the
/// domain becomes a 400 through the global exception handler.
/// </summary>
public static class RecipesEndpoints
{
    public static IEndpointRouteBuilder MapRecipesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recipes").WithTags("Recipes").RequireAuthorization().AddRequestValidation();
        group.MapGet("/", List).WithName("GetRecipes").Produces<List<RecipeDto>>();
        group.MapGet("/{recipeId:guid}", Get).WithName("GetRecipeById").Produces<RecipeDto>().ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", Create).WithName("CreateRecipe").Produces<RecipeDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        group.MapPut("/{recipeId:guid}", Update).WithName("UpdateRecipe").Produces<RecipeDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        group.MapDelete("/{recipeId:guid}", Archive).WithName("DeleteRecipe").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{recipeId:guid}/ingredients", AddIngredient).WithName("AddRecipeIngredient").Produces<RecipeDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPut("/{recipeId:guid}/ingredients/{ingredientId:guid}", UpdateIngredient).WithName("UpdateRecipeIngredient").Produces<RecipeDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        group.MapDelete("/{recipeId:guid}/ingredients/{ingredientId:guid}", RemoveIngredient).WithName("DeleteRecipeIngredient").Produces<RecipeDto>().ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{recipeId:guid}/image", GetImage).WithName("GetRecipeImage").ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPut("/{recipeId:guid}/image", UploadImage).WithName("UploadRecipeImage").Produces<RecipeDto>().WithUploadLimit();
        group.MapDelete("/{recipeId:guid}/image", DeleteImage).WithName("DeleteRecipeImage").Produces(StatusCodes.Status204NoContent);
        return app;
    }

    private static Task<Recipe?> Find(LifeOSDbContext db, HouseholdContext household, Guid recipeId, CancellationToken ct) =>
        db.Recipes.Include(recipe => recipe.Ingredients)
            .SingleOrDefaultAsync(recipe => recipe.Id == recipeId && recipe.HouseholdId == household.HouseholdId, ct);

    private static async Task<IResult> List(HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var recipes = await db.Recipes.AsNoTracking().Include(recipe => recipe.Ingredients)
            .Where(recipe => recipe.HouseholdId == household.HouseholdId)
            .OrderBy(recipe => recipe.Name).ThenBy(recipe => recipe.Id).ToListAsync(ct);
        return Results.Ok(recipes.Select(RecipeDto.From).ToList());
    }

    private static async Task<IResult> Get(Guid recipeId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct) =>
        await Find(db, household, recipeId, ct) is { } recipe ? Results.Ok(RecipeDto.From(recipe)) : Results.NotFound();

    private static async Task<IResult> Create(CreateRecipeRequest request, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var recipe = Recipe.Create(household.HouseholdId, request.Name, request.Servings, request.DurationMinutes,
            request.Tags, request.Instructions);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/recipes/{recipe.Id}", RecipeDto.From(recipe));
    }

    private static async Task<IResult> Update(Guid recipeId, UpdateRecipeRequest request, HouseholdContext household,
        LifeOSDbContext db, CancellationToken ct)
    {
        var recipe = await Find(db, household, recipeId, ct);
        if (recipe is null) return Results.NotFound();
        recipe.Update(request.Name, request.Servings, request.DurationMinutes, request.Tags, request.Instructions);
        await db.SaveChangesAsync(ct);
        return Results.Ok(RecipeDto.From(recipe));
    }

    private static async Task<IResult> Archive(Guid recipeId, HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var recipe = await Find(db, household, recipeId, ct);
        if (recipe is null) return Results.NotFound();
        recipe.Archive();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddIngredient(Guid recipeId, AddRecipeIngredientRequest request,
        HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var recipe = await Find(db, household, recipeId, ct);
        if (recipe is null) return Results.NotFound();
        // Foods belong to the same module; the household filter rejects cross-household references.
        if (!await db.FoodItems.AnyAsync(food => food.Id == request.FoodItemId
                && food.HouseholdId == household.HouseholdId && !food.IsArchived, ct))
            throw new ArgumentException("L'ingrédient doit référencer un aliment actif de votre foyer.");
        // Added through the DbSet so EF inserts the line instead of guessing its state from the key.
        db.RecipeIngredients.Add(recipe.AddIngredient(request.FoodItemId, request.Quantity, request.Unit));
        await db.SaveChangesAsync(ct);
        return Results.Ok(RecipeDto.From(recipe));
    }

    private static async Task<IResult> UpdateIngredient(Guid recipeId, Guid ingredientId, UpdateRecipeIngredientRequest request,
        HouseholdContext household, LifeOSDbContext db, CancellationToken ct)
    {
        var recipe = await Find(db, household, recipeId, ct);
        if (recipe is null || !recipe.UpdateIngredient(ingredientId, request.Quantity, request.Unit)) return Results.NotFound();
        await db.SaveChangesAsync(ct);
        return Results.Ok(RecipeDto.From(recipe));
    }

    private static async Task<IResult> RemoveIngredient(Guid recipeId, Guid ingredientId, HouseholdContext household,
        LifeOSDbContext db, CancellationToken ct)
    {
        var recipe = await Find(db, household, recipeId, ct);
        if (recipe is null || !recipe.RemoveIngredient(ingredientId)) return Results.NotFound();
        await db.SaveChangesAsync(ct);
        return Results.Ok(RecipeDto.From(recipe));
    }

    private static async Task<IResult> GetImage(Guid recipeId, HouseholdContext household, HttpContext context,
        LifeOSDbContext db, FoodImageStorage storage, CancellationToken ct)
    {
        var image = await db.Recipes.Where(r => r.Id == recipeId && r.HouseholdId == household.HouseholdId)
            .Select(r => new { r.ImagePath, r.ImageContentType }).SingleOrDefaultAsync(ct);
        return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
    }

    private static async Task<IResult> UploadImage(Guid recipeId, HttpRequest request, HouseholdContext household,
        LifeOSDbContext db, FoodImageStorage storage, ILogger<FoodImageStorage> logger, CancellationToken ct)
    {
        var recipe = await Find(db, household, recipeId, ct);
        if (recipe is null) return Results.NotFound();
        await storage.ReplaceImageAsync(request, recipe.ImagePath, stored => recipe.SetImage(stored.FileName, stored.ContentType),
            () => db.SaveChangesAsync(ct), logger, ct);
        return Results.Ok(RecipeDto.From(recipe));
    }

    private static async Task<IResult> DeleteImage(Guid recipeId, HouseholdContext household, LifeOSDbContext db,
        FoodImageStorage storage, ILogger<FoodImageStorage> logger, CancellationToken ct)
    {
        var recipe = await Find(db, household, recipeId, ct);
        if (recipe is null) return Results.NotFound();
        var oldPath = recipe.ImagePath;
        recipe.RemoveImage();
        await db.SaveChangesAsync(ct);
        storage.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove a deleted recipe image"));
        return Results.NoContent();
    }
}
