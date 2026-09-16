using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Dtos;
using LifeOS.Api.Validation;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.ComposedMeals;
using LifeOS.Application.Households;
using LifeOS.Application.Recipes;
using LifeOS.Application.WeekPlanning;
using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Api.Endpoints;

public static class WeekPlanningEndpoints
{
    public static IEndpointRouteBuilder MapWeekPlanningEndpoints(this IEndpointRouteBuilder app)
    {
        // Weeks
        var weekGroup = app.MapGroup("/api/weeks")
            .WithTags("WeekPlanning")
            .RequireAuthorization()
            .AddRequestValidation();

        weekGroup.MapGet("/", GetWeeksAsync)
            .WithName("GetWeeks")
            .Produces<List<WeekDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        weekGroup.MapGet("/{weekId}", GetWeekByIdAsync)
            .WithName("GetWeekById")
            .Produces<WeekDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        weekGroup.MapPost("/", CreateWeekAsync)
            .WithName("CreateWeek")
            .Produces<WeekDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        weekGroup.MapPut("/{weekId}/status", UpdateWeekStatusAsync)
            .WithName("UpdateWeekStatus")
            .Produces<WeekDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        weekGroup.MapDelete("/{weekId}", DeleteWeekAsync)
            .WithName("DeleteWeek")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Day Plans
        var dayGroup = app.MapGroup("/api/day-plans")
            .WithTags("WeekPlanning")
            .RequireAuthorization()
            .AddRequestValidation();

        dayGroup.MapGet("/{dayPlanId}", GetDayPlanByIdAsync)
            .WithName("GetDayPlanById")
            .Produces<DayPlanDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        dayGroup.MapPost("/{weekId}/days", CreateDayPlanAsync)
            .WithName("CreateDayPlan")
            .Produces<DayPlanDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        dayGroup.MapPut("/{dayPlanId}", UpdateDayPlanAsync)
            .WithName("UpdateDayPlan")
            .Produces<DayPlanDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        dayGroup.MapDelete("/{dayPlanId}", DeleteDayPlanAsync)
            .WithName("DeleteDayPlan")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Planned Meals
        var mealGroup = app.MapGroup("/api/planned-meals")
            .WithTags("WeekPlanning")
            .RequireAuthorization()
            .AddRequestValidation();

        mealGroup.MapGet("/{mealId}", GetPlannedMealByIdAsync)
            .WithName("GetPlannedMealById")
            .Produces<PlannedMealDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mealGroup.MapPost("/{dayPlanId}/meals", CreatePlannedMealAsync)
            .WithName("CreatePlannedMeal")
            .Produces<PlannedMealDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mealGroup.MapPut("/{mealId}/status", UpdatePlannedMealStatusAsync)
            .WithName("UpdatePlannedMealStatus")
            .Produces<PlannedMealDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mealGroup.MapPut("/{mealId}/replace", ReplacePlannedMealAsync)
            .WithName("ReplacePlannedMeal")
            .Produces<PlannedMealDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mealGroup.MapPost("/{mealId}/parts", AddPlannedMealPartAsync)
            .WithName("AddPlannedMealPart")
            .Produces<PlannedMealDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mealGroup.MapPut("/{mealId}/parts/{partId}", UpdatePlannedMealPartAsync)
            .WithName("UpdatePlannedMealPart")
            .Produces<PlannedMealDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mealGroup.MapDelete("/{mealId}/parts/{partId}", DeletePlannedMealPartAsync)
            .WithName("DeletePlannedMealPart")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mealGroup.MapDelete("/{mealId}", DeletePlannedMealAsync)
            .WithName("DeletePlannedMeal")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // Week endpoints
    private static async Task<IResult> GetWeeksAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var weeks = await weekRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

        return Results.Ok(weeks.Select(ToWeekDto).ToList());
    }

    private static async Task<IResult> GetWeekByIdAsync(
        Guid weekId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);

        return week == null ? Results.NotFound() : Results.Ok(ToWeekDto(week));
    }

    private static async Task<IResult> CreateWeekAsync(
        CreateWeekRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IDayPlanRepository dayPlanRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var week = Week.Create(householdId, request.StartsOn, request.Status);
            await weekRepository.AddAsync(week, cancellationToken);

            // Create day plans for the week (7 days)
            for (int i = 0; i < 7; i++)
            {
                var date = request.StartsOn.AddDays(i);
                var dayPlan = DayPlan.Create(week.Id, date);
                await dayPlanRepository.AddAsync(dayPlan, cancellationToken);
            }

            // Reload to get the created day plans
            var createdWeek = await weekRepository.GetByIdAsync(week.Id, householdId, cancellationToken);
            return Results.Created($"/api/weeks/{week.Id}", ToWeekDto(createdWeek!));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdateWeekStatusAsync(
        Guid weekId,
        UpdateWeekStatusRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var week = await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);

        if (week == null)
        {
            return Results.NotFound();
        }

        try
        {
            week.UpdateStatus(request.Status);
            await weekRepository.UpdateAsync(week, cancellationToken);

            return Results.Ok(ToWeekDto(week));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeleteWeekAsync(
        Guid weekId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await weekRepository.DeleteAsync(weekId, householdId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // Day Plan endpoints
    private static async Task<IResult> GetDayPlanByIdAsync(
        Guid dayPlanId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IDayPlanRepository dayPlanRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var dayPlan = await dayPlanRepository.GetByIdAsync(dayPlanId, householdId, cancellationToken);

        if (dayPlan == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToDayPlanDto(dayPlan));
    }

    private static async Task<IResult> CreateDayPlanAsync(
        Guid weekId,
        CreateDayPlanRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IWeekRepository weekRepository,
        IDayPlanRepository dayPlanRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        if (await weekRepository.GetByIdAsync(weekId, householdId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        try
        {
            var dayPlan = DayPlan.Create(weekId, request.Date, request.WorkContext, request.BikeCommute);
            await dayPlanRepository.AddAsync(dayPlan, cancellationToken);

            return Results.Created($"/api/day-plans/{dayPlan.Id}", ToDayPlanDto(dayPlan));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdateDayPlanAsync(
        Guid dayPlanId,
        UpdateDayPlanRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IDayPlanRepository dayPlanRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var dayPlan = await dayPlanRepository.GetByIdAsync(dayPlanId, householdId, cancellationToken);

        if (dayPlan == null)
        {
            return Results.NotFound();
        }

        try
        {
            dayPlan.Update(request.WorkContext, request.BikeCommute);
            await dayPlanRepository.UpdateAsync(dayPlan, cancellationToken);

            return Results.Ok(ToDayPlanDto(dayPlan));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeleteDayPlanAsync(
        Guid dayPlanId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IDayPlanRepository dayPlanRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await dayPlanRepository.DeleteAsync(dayPlanId, householdId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // Planned Meal endpoints
    private static async Task<IResult> GetPlannedMealByIdAsync(
        Guid mealId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await plannedMealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        return meal == null ? Results.NotFound() : Results.Ok(ToPlannedMealDto(meal));
    }

    private static async Task<IResult> CreatePlannedMealAsync(
        Guid dayPlanId,
        CreatePlannedMealRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IDayPlanRepository dayPlanRepository,
        IRecipeRepository recipeRepository,
        IComposedMealRepository composedMealRepository,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        if (await dayPlanRepository.GetByIdAsync(dayPlanId, householdId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        if (!await MealReferenceBelongsToHouseholdAsync(
            householdId,
            request.ComposedMealId,
            request.RecipeId,
            composedMealRepository,
            recipeRepository,
            cancellationToken))
        {
            return Results.NotFound();
        }

        try
        {
            var meal = PlannedMeal.Create(
                dayPlanId,
                request.MealType,
                request.ComposedMealId,
                request.RecipeId,
                request.Status);

            await plannedMealRepository.AddAsync(meal, cancellationToken);

            return Results.Created($"/api/planned-meals/{meal.Id}", ToPlannedMealDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdatePlannedMealStatusAsync(
        Guid mealId,
        UpdatePlannedMealStatusRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await plannedMealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal == null)
        {
            return Results.NotFound();
        }

        try
        {
            meal.UpdateStatus(request.Status);
            await plannedMealRepository.UpdateAsync(meal, cancellationToken);

            return Results.Ok(ToPlannedMealDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> ReplacePlannedMealAsync(
        Guid mealId,
        ReplacePlannedMealRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IRecipeRepository recipeRepository,
        IComposedMealRepository composedMealRepository,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await plannedMealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal == null)
        {
            return Results.NotFound();
        }

        if (!await MealReferenceBelongsToHouseholdAsync(
            householdId,
            request.ComposedMealId,
            request.RecipeId,
            composedMealRepository,
            recipeRepository,
            cancellationToken))
        {
            return Results.NotFound();
        }

        try
        {
            meal.ReplaceMeal(request.ComposedMealId, request.RecipeId);
            await plannedMealRepository.UpdateAsync(meal, cancellationToken);

            return Results.Ok(ToPlannedMealDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> AddPlannedMealPartAsync(
        Guid mealId,
        AddPlannedMealPartRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IHouseholdRepository householdRepository,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await plannedMealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal is null ||
            !await householdRepository.MemberProfileBelongsToHouseholdAsync(householdId, request.MemberProfileId, cancellationToken))
        {
            return Results.NotFound();
        }

        try
        {
            meal.AddPart(request.MemberProfileId, request.PortionMultiplier);
            await plannedMealRepository.UpdateAsync(meal, cancellationToken);

            return Results.Ok(ToPlannedMealDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdatePlannedMealPartAsync(
        Guid mealId,
        Guid partId,
        UpdatePlannedMealPartRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await plannedMealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal is null)
        {
            return Results.NotFound();
        }

        try
        {
            if (!meal.UpdatePart(partId, request.PortionMultiplier))
            {
                return Results.NotFound();
            }

            await plannedMealRepository.UpdateAsync(meal, cancellationToken);

            return Results.Ok(ToPlannedMealDto(meal));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeletePlannedMealPartAsync(
        Guid mealId,
        Guid partId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var meal = await plannedMealRepository.GetByIdAsync(mealId, householdId, cancellationToken);

        if (meal is null || !meal.RemovePart(partId))
        {
            return Results.NotFound();
        }

        await plannedMealRepository.UpdateAsync(meal, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeletePlannedMealAsync(
        Guid mealId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        IPlannedMealRepository plannedMealRepository,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await plannedMealRepository.DeleteAsync(mealId, householdId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<bool> MealReferenceBelongsToHouseholdAsync(
        Guid householdId,
        Guid? composedMealId,
        Guid? recipeId,
        IComposedMealRepository composedMealRepository,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        if (recipeId.HasValue && recipeId.Value != Guid.Empty)
        {
            return await recipeRepository.GetByIdAsync(recipeId.Value, householdId, cancellationToken) is not null;
        }

        if (composedMealId.HasValue && composedMealId.Value != Guid.Empty)
        {
            return await composedMealRepository.GetByIdAsync(composedMealId.Value, householdId, cancellationToken) is not null;
        }

        return true;
    }

    // DTO helpers
    private static WeekDto ToWeekDto(Week week)
    {
        return new WeekDto(
            week.Id,
            week.StartsOn,
            week.Status,
            week.DayPlans.Select(ToDayPlanDto).ToList(),
            week.CreatedAt,
            week.UpdatedAt);
    }

    private static DayPlanDto ToDayPlanDto(DayPlan dayPlan)
    {
        return new DayPlanDto(
            dayPlan.Id,
            dayPlan.Date,
            dayPlan.WorkContext,
            dayPlan.BikeCommute,
            dayPlan.PlannedMeals.Select(ToPlannedMealDto).ToList());
    }

    private static PlannedMealDto ToPlannedMealDto(PlannedMeal meal)
    {
        return new PlannedMealDto(
            meal.Id,
            meal.MealType,
            meal.Status,
            meal.ComposedMealId,
            meal.RecipeId,
            meal.Parts.Select(p => new PlannedMealPartDto(p.Id, p.MemberProfileId, p.PortionMultiplier)).ToList(),
            meal.CreatedAt,
            meal.UpdatedAt);
    }
}
