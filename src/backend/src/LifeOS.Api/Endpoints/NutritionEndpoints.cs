using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Validation;
using LifeOS.Application.Households;
using LifeOS.Domain.Households;
using LifeOS.Domain.WeekPlanning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public static class NutritionEndpoints
{
    public static IEndpointRouteBuilder MapNutritionEndpoints(this IEndpointRouteBuilder app)
    {
        // User Configuration
        var configGroup = app.MapGroup("/api/nutrition/configuration")
            .WithTags("Nutrition")
            .RequireAuthorization()
            .AddRequestValidation();

        configGroup.MapGet("/", GetUserConfigurationAsync)
            .WithName("GetUserConfiguration")
            .Produces<UserConfigurationDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        configGroup.MapPost("/", UpsertUserConfigurationAsync)
            .WithName("UpsertUserConfiguration")
            .Produces<UserConfigurationDto>()
            .Produces<UserConfigurationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Activity Sessions
        var activityGroup = app.MapGroup("/api/activity-sessions")
            .WithTags("Nutrition")
            .RequireAuthorization()
            .AddRequestValidation();

        activityGroup.MapGet("/day/{dayPlanId}", GetActivitySessionsForDayAsync)
            .WithName("GetActivitySessionsForDay")
            .Produces<List<ActivitySessionDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        activityGroup.MapPost("/day/{dayPlanId}", CreateActivitySessionAsync)
            .WithName("CreateActivitySession")
            .Produces<ActivitySessionDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        activityGroup.MapPut("/{sessionId}", UpdateActivitySessionAsync)
            .WithName("UpdateActivitySession")
            .Produces<ActivitySessionDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        activityGroup.MapDelete("/{sessionId}", DeleteActivitySessionAsync)
            .WithName("DeleteActivitySession")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Nutrition calculations
        var calcGroup = app.MapGroup("/api/nutrition/calculations")
            .WithTags("Nutrition")
            .RequireAuthorization();

        calcGroup.MapGet("/day/{dayPlanId}", GetDailyNutritionTargetAsync)
            .WithName("GetDailyNutritionTarget")
            .Produces<DailyNutritionTargetDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // User Configuration endpoints
    private static async Task<IResult> GetUserConfigurationAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        LifeOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var config = await dbContext.UserConfigurations
            .FirstOrDefaultAsync(c => c.HouseholdId == householdId, cancellationToken);

        if (config == null)
        {
            return Results.NotFound();
        }

        var dto = new UserConfigurationDto(
            config.Id,
            config.HouseholdId,
            config.DailyBaseEnergyKcal,
            config.TargetNetDeficitKcal,
            config.TargetProteinG,
            config.TargetCarbsG,
            config.TargetFatsG,
            config.CreatedAt,
            config.UpdatedAt);

        return Results.Ok(dto);
    }

    private static async Task<IResult> UpsertUserConfigurationAsync(
        UpsertUserConfigurationRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        LifeOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var existingConfig = await dbContext.UserConfigurations
                .FirstOrDefaultAsync(c => c.HouseholdId == householdId, cancellationToken);

            if (existingConfig != null)
            {
                existingConfig.UpdateConfiguration(
                    request.DailyBaseEnergyKcal,
                    request.TargetNetDeficitKcal,
                    request.TargetProteinG,
                    request.TargetCarbsG,
                    request.TargetFatsG);

                await dbContext.SaveChangesAsync(cancellationToken);

                var updatedDto = new UserConfigurationDto(
                    existingConfig.Id,
                    existingConfig.HouseholdId,
                    existingConfig.DailyBaseEnergyKcal,
                    existingConfig.TargetNetDeficitKcal,
                    existingConfig.TargetProteinG,
                    existingConfig.TargetCarbsG,
                    existingConfig.TargetFatsG,
                    existingConfig.CreatedAt,
                    existingConfig.UpdatedAt);

                return Results.Ok(updatedDto);
            }

            var config = UserConfiguration.Create(
                householdId,
                request.DailyBaseEnergyKcal,
                request.TargetNetDeficitKcal,
                request.TargetProteinG,
                request.TargetCarbsG,
                request.TargetFatsG);

            await dbContext.UserConfigurations.AddAsync(config, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var dto = new UserConfigurationDto(
                config.Id,
                config.HouseholdId,
                config.DailyBaseEnergyKcal,
                config.TargetNetDeficitKcal,
                config.TargetProteinG,
                config.TargetCarbsG,
                config.TargetFatsG,
                config.CreatedAt,
                config.UpdatedAt);

            return Results.Created($"/api/nutrition/configuration", dto);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // Activity Session endpoints
    private static async Task<IResult> GetActivitySessionsForDayAsync(
        Guid dayPlanId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        LifeOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        // Verify day plan belongs to this household
        var dayPlan = await dbContext.DayPlans.FindAsync(new object[] { dayPlanId }, cancellationToken: cancellationToken);
        if (dayPlan == null)
        {
            return Results.NotFound();
        }

        var week = await dbContext.Weeks.FindAsync(new object[] { dayPlan.WeekId }, cancellationToken: cancellationToken);
        if (week == null || week.HouseholdId != householdId)
        {
            return Results.Forbid();
        }

        var sessions = await dbContext.ActivitySessions
            .Where(s => s.DayPlanId == dayPlanId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = sessions.Select(s => new ActivitySessionDto(
            s.Id,
            s.DayPlanId,
            s.Type,
            s.Intensity,
            s.DurationMinutes,
            s.EstimatedEnergyKcal,
            s.CreatedAt,
            s.UpdatedAt)).ToList();

        return Results.Ok(dtos);
    }

    private static async Task<IResult> CreateActivitySessionAsync(
        Guid dayPlanId,
        CreateActivitySessionRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        LifeOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        // Verify day plan belongs to this household
        var dayPlan = await dbContext.DayPlans.FindAsync(new object[] { dayPlanId }, cancellationToken: cancellationToken);
        if (dayPlan == null)
        {
            return Results.NotFound();
        }

        var week = await dbContext.Weeks.FindAsync(new object[] { dayPlan.WeekId }, cancellationToken: cancellationToken);
        if (week == null || week.HouseholdId != householdId)
        {
            return Results.Forbid();
        }

        try
        {
            var session = ActivitySession.Create(
                dayPlanId,
                request.Type,
                request.Intensity,
                request.DurationMinutes,
                request.EstimatedEnergyKcal);

            await dbContext.ActivitySessions.AddAsync(session, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var dto = new ActivitySessionDto(
                session.Id,
                session.DayPlanId,
                session.Type,
                session.Intensity,
                session.DurationMinutes,
                session.EstimatedEnergyKcal,
                session.CreatedAt,
                session.UpdatedAt);

            return Results.Created($"/api/activity-sessions/{session.Id}", dto);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdateActivitySessionAsync(
        Guid sessionId,
        UpdateActivitySessionRequest request,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        LifeOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var session = await dbContext.ActivitySessions.FindAsync(new object[] { sessionId }, cancellationToken: cancellationToken);
        if (session == null)
        {
            return Results.NotFound();
        }

        // Verify session's day plan belongs to this household
        var dayPlan = await dbContext.DayPlans.FindAsync(new object[] { session.DayPlanId }, cancellationToken: cancellationToken);
        if (dayPlan == null)
        {
            return Results.NotFound();
        }

        var week = await dbContext.Weeks.FindAsync(new object[] { dayPlan.WeekId }, cancellationToken: cancellationToken);
        if (week == null || week.HouseholdId != householdId)
        {
            return Results.Forbid();
        }

        try
        {
            session.UpdateDetails(
                request.Type,
                request.Intensity,
                request.DurationMinutes,
                request.EstimatedEnergyKcal);

            await dbContext.SaveChangesAsync(cancellationToken);

            var dto = new ActivitySessionDto(
                session.Id,
                session.DayPlanId,
                session.Type,
                session.Intensity,
                session.DurationMinutes,
                session.EstimatedEnergyKcal,
                session.CreatedAt,
                session.UpdatedAt);

            return Results.Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeleteActivitySessionAsync(
        Guid sessionId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        LifeOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        var session = await dbContext.ActivitySessions.FindAsync(new object[] { sessionId }, cancellationToken: cancellationToken);
        if (session == null)
        {
            return Results.NotFound();
        }

        // Verify session's day plan belongs to this household
        var dayPlan = await dbContext.DayPlans.FindAsync(new object[] { session.DayPlanId }, cancellationToken: cancellationToken);
        if (dayPlan == null)
        {
            return Results.NotFound();
        }

        var week = await dbContext.Weeks.FindAsync(new object[] { dayPlan.WeekId }, cancellationToken: cancellationToken);
        if (week == null || week.HouseholdId != householdId)
        {
            return Results.Forbid();
        }

        dbContext.ActivitySessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // Nutrition calculation endpoint
    private static async Task<IResult> GetDailyNutritionTargetAsync(
        Guid dayPlanId,
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        LifeOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        // Verify day plan belongs to this household
        var dayPlan = await dbContext.DayPlans.FindAsync(new object[] { dayPlanId }, cancellationToken: cancellationToken);
        if (dayPlan == null)
        {
            return Results.NotFound();
        }

        var week = await dbContext.Weeks.FindAsync(new object[] { dayPlan.WeekId }, cancellationToken: cancellationToken);
        if (week == null || week.HouseholdId != householdId)
        {
            return Results.Forbid();
        }

        // Get user configuration
        var config = await dbContext.UserConfigurations
            .FirstOrDefaultAsync(c => c.HouseholdId == householdId, cancellationToken);

        if (config == null)
        {
            return Results.Problem("User configuration not set up", statusCode: StatusCodes.Status400BadRequest);
        }

        // Sum activity energy for the day
        var activityEnergy = await dbContext.ActivitySessions
            .Where(s => s.DayPlanId == dayPlanId)
            .SumAsync(s => s.EstimatedEnergyKcal, cancellationToken);

        // Compute daily food target
        var dailyFoodTarget = config.ComputeDailyFoodTarget(activityEnergy);

        var result = new DailyNutritionTargetDto(
            dayPlanId,
            config.DailyBaseEnergyKcal,
            activityEnergy,
            config.TargetNetDeficitKcal,
            dailyFoodTarget,
            config.TargetProteinG,
            config.TargetCarbsG,
            config.TargetFatsG);

        return Results.Ok(result);
    }
}
