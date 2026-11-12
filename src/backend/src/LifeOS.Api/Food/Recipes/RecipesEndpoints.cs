using LifeOS.Api.Food.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Domain.Food.Recipes;
using LifeOS.Infrastructure.Food.Files;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Food.Recipes;

/// <summary>
/// Simple management screen (ADR 0009): HTTP glue over <see cref="RecipeLibrary"/>, shared with
/// the assistant; validity rules live in <see cref="Recipe"/>. Invalid input raised by the
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

    private static IResult Ok(Recipe? recipe) => recipe is null ? Results.NotFound() : Results.Ok(RecipeDto.From(recipe));

    private static async Task<IResult> List(HouseholdContext household, RecipeLibrary recipes, CancellationToken ct) =>
        Results.Ok((await recipes.ListAsync(household.HouseholdId, ct: ct)).Select(RecipeDto.From).ToList());

    private static async Task<IResult> Get(Guid recipeId, HouseholdContext household, RecipeLibrary recipes, CancellationToken ct) =>
        Ok(await recipes.FindAsync(household.HouseholdId, recipeId, ct));

    private static async Task<IResult> Create(CreateRecipeRequest request, HouseholdContext household, RecipeLibrary recipes, CancellationToken ct)
    {
        var recipe = await recipes.CreateAsync(household.HouseholdId, request, null, ct);
        return Results.Created($"/api/recipes/{recipe.Id}", RecipeDto.From(recipe));
    }

    private static async Task<IResult> Update(Guid recipeId, UpdateRecipeRequest request, HouseholdContext household,
        RecipeLibrary recipes, CancellationToken ct) =>
        Ok(await recipes.UpdateAsync(household.HouseholdId, recipeId, request, ct));

    private static async Task<IResult> Archive(Guid recipeId, HouseholdContext household, RecipeLibrary recipes, CancellationToken ct) =>
        await recipes.ArchiveAsync(household.HouseholdId, recipeId, ct) is null ? Results.NotFound() : Results.NoContent();

    private static async Task<IResult> AddIngredient(Guid recipeId, AddRecipeIngredientRequest request,
        HouseholdContext household, RecipeLibrary recipes, CancellationToken ct) =>
        Ok(await recipes.AddIngredientAsync(household.HouseholdId, recipeId, request, ct));

    private static async Task<IResult> UpdateIngredient(Guid recipeId, Guid ingredientId, UpdateRecipeIngredientRequest request,
        HouseholdContext household, RecipeLibrary recipes, CancellationToken ct) =>
        Ok(await recipes.UpdateIngredientAsync(household.HouseholdId, recipeId, ingredientId, request, ct));

    private static async Task<IResult> RemoveIngredient(Guid recipeId, Guid ingredientId, HouseholdContext household,
        RecipeLibrary recipes, CancellationToken ct) =>
        Ok(await recipes.RemoveIngredientAsync(household.HouseholdId, recipeId, ingredientId, ct));

    private static async Task<IResult> GetImage(Guid recipeId, HouseholdContext household, HttpContext context,
        LifeOSDbContext db, FoodImageStorage storage, CancellationToken ct)
    {
        var image = await db.Recipes.Where(r => r.Id == recipeId && r.HouseholdId == household.HouseholdId)
            .Select(r => new { r.ImagePath, r.ImageContentType }).SingleOrDefaultAsync(ct);
        return storage.Serve(context, image?.ImagePath, image?.ImageContentType);
    }

    private static async Task<IResult> UploadImage(Guid recipeId, HttpRequest request, HouseholdContext household,
        RecipeLibrary recipes, CancellationToken ct)
    {
        var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
        await using var content = file.OpenReadStream();
        return Ok(await recipes.ReplaceImageAsync(household.HouseholdId, recipeId, content, file.Length, file.ContentType, ct));
    }

    private static async Task<IResult> DeleteImage(Guid recipeId, HouseholdContext household, RecipeLibrary recipes, CancellationToken ct) =>
        await recipes.RemoveImageAsync(household.HouseholdId, recipeId, ct) is null ? Results.NotFound() : Results.NoContent();
}
