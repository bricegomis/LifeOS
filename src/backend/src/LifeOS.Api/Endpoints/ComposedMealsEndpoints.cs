using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Dtos;
using LifeOS.Application.ComposedMeals;
using LifeOS.Application.Households;
using LifeOS.Domain.ComposedMeals;

namespace LifeOS.Api.Endpoints;

public static class ComposedMealsEndpoints
{
    public static IEndpointRouteBuilder MapComposedMealsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/composed-meals")
            .WithTags("ComposedMeals")
            .RequireAuthorization();

        group.MapGet("/", GetComposedMealsAsync)
            .WithName("GetComposedMeals")
            .WithOpenApi();

        group.MapGet("/{mealId}", GetComposedMealByIdAsync)
            .WithName("GetComposedMealById")
            .WithOpenApi();

        group.MapPost("/", CreateComposedMealAsync)
            .WithName("CreateComposedMeal")
            .WithOpenApi();

        group.MapPut("/{mealId}", UpdateComposedMealAsync)
            .WithName("UpdateComposedMeal")
            .WithOpenApi();

        group.MapDelete("/{mealId}", DeleteComposedMealAsync)
            .WithName("DeleteComposedMeal")
            .WithOpenApi();

        group.MapPost("/{mealId}/parts", AddComposedMealPartAsync)
            .WithName("AddComposedMealPart")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetComposedMealsAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IComposedMealRepository mealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meals = await mealRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

        return Results.Ok(meals.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetComposedMealByIdAsync(
        Guid mealId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IComposedMealRepository mealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await mealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToDto(meal));
    }

    private static async Task<IResult> CreateComposedMealAsync(
        CreateComposedMealRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IComposedMealRepository mealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var meal = ComposedMeal.Create(householdId, request.Name);
            await mealRepository.AddAsync(meal, cancellationToken);

            return Results.Created($"/api/composed-meals/{meal.Id}", ToDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateComposedMealAsync(
        Guid mealId,
        UpdateComposedMealRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IComposedMealRepository mealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await mealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal == null)
        {
            return Results.NotFound();
        }

        try
        {
            meal.Update(request.Name);
            await mealRepository.UpdateAsync(meal, cancellationToken);

            return Results.Ok(ToDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteComposedMealAsync(
        Guid mealId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IComposedMealRepository mealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await mealRepository.DeleteAsync(mealId, householdId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AddComposedMealPartAsync(
        Guid mealId,
        AddComposedMealPartRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IComposedMealRepository mealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await mealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal == null)
        {
            return Results.NotFound();
        }

        try
        {
            meal.AddPart(request.RecipeId, request.QuantityFactor);
            await mealRepository.UpdateAsync(meal, cancellationToken);

            return Results.Ok(ToDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static ComposedMealDto ToDto(ComposedMeal meal)
    {
        return new ComposedMealDto(
            meal.Id,
            meal.Name,
            meal.Parts.Select(p => new ComposedMealPartDto(p.Id, p.RecipeId, p.QuantityFactor)).ToList(),
            meal.CreatedAt,
            meal.UpdatedAt);
    }
}
