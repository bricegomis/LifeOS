using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Dtos;
using LifeOS.Application.Households;
using LifeOS.Application.Recipes;
using LifeOS.Domain.Recipes;

namespace LifeOS.Api.Endpoints;

public static class RecipesEndpoints
{
    public static IEndpointRouteBuilder MapRecipesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recipes")
            .WithTags("Recipes")
            .RequireAuthorization();

        group.MapGet("/", GetRecipesAsync)
            .WithName("GetRecipes")
            .WithOpenApi();

        group.MapGet("/{recipeId}", GetRecipeByIdAsync)
            .WithName("GetRecipeById")
            .WithOpenApi();

        group.MapPost("/", CreateRecipeAsync)
            .WithName("CreateRecipe")
            .WithOpenApi();

        group.MapPut("/{recipeId}", UpdateRecipeAsync)
            .WithName("UpdateRecipe")
            .WithOpenApi();

        group.MapDelete("/{recipeId}", DeleteRecipeAsync)
            .WithName("DeleteRecipe")
            .WithOpenApi();

        group.MapPost("/{recipeId}/ingredients", AddRecipeIngredientAsync)
            .WithName("AddRecipeIngredient")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetRecipesAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var recipes = await recipeRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

        return Results.Ok(recipes.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetRecipeByIdAsync(
        Guid recipeId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var recipe = await recipeRepository.GetByIdAsync(recipeId, householdId, cancellationToken);

        if (recipe == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToDto(recipe));
    }

    private static async Task<IResult> CreateRecipeAsync(
        CreateRecipeRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var recipe = Recipe.Create(
                householdId,
                request.Name,
                request.Servings,
                request.DurationMinutes,
                request.Tags,
                request.Metadata);

            await recipeRepository.AddAsync(recipe, cancellationToken);

            return Results.Created($"/api/recipes/{recipe.Id}", ToDto(recipe));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateRecipeAsync(
        Guid recipeId,
        UpdateRecipeRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var recipe = await recipeRepository.GetByIdAsync(recipeId, householdId, cancellationToken);

        if (recipe == null)
        {
            return Results.NotFound();
        }

        try
        {
            recipe.Update(request.Name, request.Servings, request.DurationMinutes, request.Tags, request.Metadata);
            await recipeRepository.UpdateAsync(recipe, cancellationToken);

            return Results.Ok(ToDto(recipe));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteRecipeAsync(
        Guid recipeId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await recipeRepository.DeleteAsync(recipeId, householdId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AddRecipeIngredientAsync(
        Guid recipeId,
        AddRecipeIngredientRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var recipe = await recipeRepository.GetByIdAsync(recipeId, householdId, cancellationToken);

        if (recipe == null)
        {
            return Results.NotFound();
        }

        try
        {
            recipe.AddIngredient(request.FoodItemId, request.Quantity, request.Unit);
            await recipeRepository.UpdateAsync(recipe, cancellationToken);

            return Results.Ok(ToDto(recipe));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static RecipeDto ToDto(Recipe recipe)
    {
        return new RecipeDto(
            recipe.Id,
            recipe.Name,
            recipe.Servings,
            recipe.DurationMinutes,
            recipe.Tags,
            recipe.Metadata,
            recipe.Ingredients.Select(i => new RecipeIngredientDto(i.Id, i.FoodItemId, i.Quantity, i.Unit)).ToList(),
            recipe.CreatedAt,
            recipe.UpdatedAt);
    }
}
