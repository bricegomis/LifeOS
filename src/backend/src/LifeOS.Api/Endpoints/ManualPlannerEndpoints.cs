using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Dtos;
using LifeOS.Application.Households;
using LifeOS.Domain.WeekPlanning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Endpoints;

public static class ManualPlannerEndpoints
{
    public static IEndpointRouteBuilder MapManualPlannerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/manual-planner").RequireAuthorization().WithTags("Manual planner");
        group.MapGet("/weeks", ListWeeksAsync).Produces<List<ManualWeekSummary>>();
        group.MapPost("/weeks", CreateWeekAsync).Produces<ManualWeekDto>(201);
        group.MapGet("/weeks/{id:guid}", GetWeekAsync).Produces<ManualWeekDto>();
        group.MapPost("/meals", CreateMealAsync).Produces<ManualMealDto>(201);
        group.MapPut("/meals/{id:guid}", UpdateMealAsync).Produces<ManualMealDto>();
        group.MapDelete("/meals/{id:guid}", DeleteMealAsync);
        group.MapPost("/sports", CreateSportAsync).Produces<ManualSportDto>(201);
        group.MapPut("/sports/{id:guid}", UpdateSportAsync).Produces<ManualSportDto>();
        group.MapDelete("/sports/{id:guid}", DeleteSportAsync);
        return app;
    }

    private static async Task<Guid?> Household(ClaimsPrincipal user, ResolveHouseholdForUserQuery households, CancellationToken ct) =>
        user.TryGetUserId(out var sub) ? await households.ExecuteAsync(sub, ct) : null;

    private static Task<bool> OwnsDay(LifeOSDbContext db, Guid household, Guid dayId, CancellationToken ct) =>
        db.DayPlans.AnyAsync(d => d.Id == dayId && db.Weeks.Any(w => w.Id == d.WeekId && w.HouseholdId == household), ct);

    private static async Task<IResult> ListWeeksAsync(ClaimsPrincipal user, ResolveHouseholdForUserQuery households,
        LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        return Results.Ok(await db.Weeks.Where(w => w.HouseholdId == household)
            .OrderByDescending(w => w.StartsOn).ThenByDescending(w => w.IsManual)
            .Select(w => new ManualWeekSummary(w.Id, w.StartsOn, w.TimeZoneId, w.IsManual)).ToListAsync(ct));
    }

    private static async Task<IResult> CreateWeekAsync(ManualWeekRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        if (await db.Weeks.AnyAsync(w => w.HouseholdId == household && w.StartsOn == request.StartsOn, ct))
            return Results.Problem("Une semaine existe déjà à cette date. Ouvrez-la depuis le semainier.", statusCode: 409);
        var week = Week.CreateManual(household.Value, request.StartsOn, request.TimeZoneId);
        db.Weeks.Add(week);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/manual-planner/weeks/{week.Id}", await WeekDto(db, week, ct));
    }

    private static async Task<IResult> GetWeekAsync(Guid id, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var week = await db.Weeks.Include(w => w.DayPlans).ThenInclude(d => d.PlannedMeals).ThenInclude(m => m.FoodLines)
            .SingleOrDefaultAsync(w => w.Id == id && w.HouseholdId == household, ct);
        return week is null ? Results.NotFound() : Results.Ok(await WeekDto(db, week, ct));
    }

    private static async Task<ManualWeekDto> WeekDto(LifeOSDbContext db, Week week, CancellationToken ct)
    {
        var days = new List<ManualDayDto>();
        var dayIds = week.DayPlans.Select(d => d.Id).ToList();
        var sessions = await db.ActivitySessions.Where(s => dayIds.Contains(s.DayPlanId)).ToListAsync(ct);
        foreach (var day in week.DayPlans.OrderBy(d => d.Date))
        {
            var meals = new List<ManualMealDto>();
            foreach (var meal in day.PlannedMeals.Where(m => m.Status != "skipped"))
                meals.Add(await MealDto(db, meal, week.HouseholdId, ct));
            days.Add(new(day.Id, day.Date, meals, sessions.Where(s => s.DayPlanId == day.Id).Select(SportDto).ToList(),
                SumNutrition(meals.Select(m => m.Nutrition).ToList())));
        }
        return new(week.Id, week.StartsOn, week.TimeZoneId, week.IsManual, days);
    }

    private static async Task<IResult> CreateMealAsync(ManualMealRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        if (!await OwnsDay(db, household.Value, request.DayPlanId, ct)) return Results.NotFound();
        if (request.StartMinute is null) throw new ArgumentException("Placez le nouvel événement dans un créneau.");
        var meal = PlannedMeal.CreateManual(request.DayPlanId);
        meal.Schedule(request.DayPlanId, request.StartMinute, request.EndMinute, request.PersonalPortion, request.ChildrenCount);
        await ReplaceContent(db, meal, household.Value, request, ct);
        db.PlannedMeals.Add(meal);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/manual-planner/meals/{meal.Id}", await MealDto(db, meal, household.Value, ct));
    }

    private static async Task<IResult> UpdateMealAsync(Guid id, ManualMealRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var meal = await db.PlannedMeals.Include(m => m.FoodLines).SingleOrDefaultAsync(m => m.Id == id, ct);
        if (meal is null || !await OwnsDay(db, household.Value, meal.DayPlanId, ct)
            || !await OwnsDay(db, household.Value, request.DayPlanId, ct)) return Results.NotFound();
        if (request.ReplaceContent) await ReplaceContent(db, meal, household.Value, request, ct);
        else if (meal.ContentName is null)
        {
            var content = await RecipeContent(db, meal.Id, household.Value, meal.RecipeId, meal.ComposedMealId, ct);
            meal.SetContent(content.Name, meal.RecipeId, meal.ComposedMealId, content.Lines);
        }
        else
        {
            if (request.RecipeId != meal.RecipeId) throw new ArgumentException("Le changement de contenu exige un remplacement explicite.");
            if (request.Lines is { } lines && meal.RecipeId is null && meal.ComposedMealId is null)
            {
                if (lines.Count != meal.FoodLines.Count || lines.Select(l => l.SnapshotLineId).Distinct().Count() != lines.Count)
                    throw new ArgumentException("Pour ajouter ou retirer un produit, remplacez explicitement le contenu.");
                foreach (var line in lines)
                {
                    var existing = meal.FoodLines.SingleOrDefault(l => l.Id == line.SnapshotLineId);
                    if (existing is null || line.FoodItemId != existing.FoodItemId || line.Unit != existing.Unit)
                        throw new ArgumentException("Le changement de produit ou d'unité exige un remplacement explicite.");
                    existing.UpdateQuantity(line.Quantity);
                }
            }
        }
        meal.Schedule(request.DayPlanId, request.StartMinute, request.EndMinute, request.PersonalPortion, request.ChildrenCount);
        await db.SaveChangesAsync(ct);
        return Results.Ok(await MealDto(db, meal, household.Value, ct));
    }

    private static async Task ReplaceContent(LifeOSDbContext db, PlannedMeal meal, Guid household,
        ManualMealRequest request, CancellationToken ct)
    {
        if (request.RecipeId is { } recipeId)
        {
            if (request.Lines is { Count: > 0 }) throw new ArgumentException("Choisissez une recette OU des produits.");
            if (!await db.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == household && !r.IsArchived, ct))
                throw new ArgumentException("Recette indisponible dans votre bibliothèque.");
            var content = await RecipeContent(db, meal.Id, household, recipeId, null, ct);
            meal.SetContent(content.Name, recipeId, null, content.Lines);
        }
        else
        {
            if (request.Lines is not { Count: > 0 and <= 100 }) throw new ArgumentException("Ajoutez entre 1 et 100 produits.");
            var lines = new List<MealFoodLine>();
            foreach (var input in request.Lines)
            {
                var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == input.FoodItemId && f.HouseholdId == household && !f.IsArchived, ct);
                if (food is null) throw new ArgumentException("Produit indisponible dans votre bibliothèque.");
                lines.Add(MealFoodLine.Snapshot(meal.Id, food.Id, food.Name, input.Quantity, input.Unit, food.ReferenceUnit, food.Nutrition));
            }
            meal.SetContent(request.Name ?? string.Join(", ", lines.Select(l => l.Name)), null, null, lines);
        }
    }

    private static async Task<(string Name, List<MealFoodLine> Lines)> RecipeContent(LifeOSDbContext db,
        Guid mealId, Guid household, Guid? recipeId, Guid? composedId, CancellationToken ct)
    {
        var recipes = new List<(Guid Id, decimal Factor)>();
        var name = "Ancien repas — référence indisponible";
        if (recipeId is { } recipe) recipes.Add((recipe, 1));
        if (composedId is { } composed)
        {
            var source = await db.ComposedMeals.Include(c => c.Parts).SingleOrDefaultAsync(c => c.Id == composed && c.HouseholdId == household, ct);
            if (source is not null)
            {
                name = source.Name;
                recipes.AddRange(source.Parts.Select(p => (p.RecipeId, p.QuantityFactor)));
            }
        }
        var lines = new List<MealFoodLine>();
        foreach (var (id, factor) in recipes)
        {
            var source = await db.Recipes.Include(r => r.Ingredients).SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == household, ct);
            if (source is null) continue;
            if (composedId is null) name = source.Name;
            foreach (var ingredient in source.Ingredients)
            {
                var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == ingredient.FoodItemId && f.HouseholdId == household, ct);
                lines.Add(MealFoodLine.Snapshot(mealId, food?.Id, food?.Name ?? "Ingrédient indisponible",
                    ingredient.Quantity * factor / source.Servings, ingredient.Unit, food?.ReferenceUnit ?? "", food?.Nutrition));
            }
        }
        return (name, lines);
    }

    private static async Task<ManualMealDto> MealDto(LifeOSDbContext db, PlannedMeal meal, Guid household, CancellationToken ct)
    {
        var snapshot = meal.ContentName is not null;
        var content = snapshot ? (Name: meal.ContentName!, Lines: meal.FoodLines)
            : await RecipeContent(db, meal.Id, household, meal.RecipeId, meal.ComposedMealId, ct);
        var warnings = new List<string>();
        if (!snapshot) warnings.Add("Repas historique : valeurs actuelles non figées. Le placement manuel les enregistrera.");
        if (content.Lines.Count == 0) warnings.Add("Aucun ingrédient nutritionnel disponible.");
        double? Total(Func<MealFoodLine, double?> select, string label)
        {
            var values = content.Lines.Select(l =>
            {
                var value = select(l);
                var factor = l.NutritionFactor();
                if (value is null || factor is null) warnings.Add($"{l.Name} : {label} inconnu ou unité incompatible.");
                return value * factor * (double)meal.PersonalPortion;
            }).ToList();
            return values.Count > 0 && values.All(v => v is not null) ? values.Sum(v => v!.Value) : null;
        }
        var calories = Total(l => l.Calories, "énergie");
        var protein = Total(l => l.Protein, "protéines");
        var carbs = Total(l => l.Carbs, "glucides");
        var fat = Total(l => l.Fat, "lipides");
        var nutrition = new PersonalNutritionDto(calories, protein, carbs, fat, warnings.Count == 0, warnings.Distinct().ToList());
        return new(meal.Id, meal.DayPlanId, content.Name, meal.StartMinute, meal.EndMinute, meal.RecipeId,
            meal.ComposedMealId, meal.PersonalPortion, meal.ChildrenCount, snapshot,
            content.Lines.Select(l => new MealLineDto(l.Id, l.FoodItemId, l.Name, l.Quantity, l.Unit,
                l.Quantity * meal.PersonalPortion, l.Quantity * meal.PersonalPortion * meal.PreparationFactor,
                l.ReferenceUnit, l.Calories, l.Protein, l.Carbs, l.Fat)).ToList(), nutrition);
    }

    private static PersonalNutritionDto SumNutrition(List<PersonalNutritionDto> meals)
    {
        double? Sum(Func<PersonalNutritionDto, double?> select) =>
            meals.All(m => select(m) is not null) ? meals.Sum(m => select(m)!.Value) : null;
        return new(Sum(m => m.Calories), Sum(m => m.Protein), Sum(m => m.Carbs), Sum(m => m.Fat),
            meals.All(m => m.IsComplete), meals.SelectMany(m => m.Warnings).Distinct().ToList());
    }

    private static async Task<IResult> DeleteMealAsync(Guid id, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var meal = await db.PlannedMeals.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (meal is null || !await OwnsDay(db, household.Value, meal.DayPlanId, ct)) return Results.NotFound();
        db.PlannedMeals.Remove(meal);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateSportAsync(ManualSportRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        if (!await OwnsDay(db, household.Value, request.DayPlanId, ct)) return Results.NotFound();
        if (request.StartMinute is null) throw new ArgumentException("Placez la nouvelle séance dans un créneau.");
        await ValidateTemplate(db, household.Value, request.SportTemplateId, ct);
        var session = ActivitySession.Create(request.DayPlanId, request.Sport, request.Intensity, request.DurationMinutes, request.Calories);
        session.Schedule(request.DayPlanId, request.StartMinute, request.EndMinute, request.Name, request.Sport,
            request.Intensity, request.DurationMinutes, request.DistanceKm, request.Calories, request.SportTemplateId);
        db.ActivitySessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/manual-planner/sports/{session.Id}", SportDto(session));
    }

    private static async Task<IResult> UpdateSportAsync(Guid id, ManualSportRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var session = await db.ActivitySessions.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (session is null || !await OwnsDay(db, household.Value, session.DayPlanId, ct)
            || !await OwnsDay(db, household.Value, request.DayPlanId, ct)) return Results.NotFound();
        if (request.SportTemplateId != session.SportTemplateId)
        {
            if (!request.ReplaceContent) throw new ArgumentException("Remplacer le modèle doit être explicite.");
            await ValidateTemplate(db, household.Value, request.SportTemplateId, ct);
        }
        session.Schedule(request.DayPlanId, request.StartMinute, request.EndMinute, request.Name, request.Sport,
            request.Intensity, request.DurationMinutes, request.DistanceKm, request.Calories, request.SportTemplateId);
        await db.SaveChangesAsync(ct);
        return Results.Ok(SportDto(session));
    }

    private static async Task ValidateTemplate(LifeOSDbContext db, Guid household, Guid? templateId, CancellationToken ct)
    {
        if (templateId is null || !await db.SportTemplates.AnyAsync(s => s.Id == templateId && s.HouseholdId == household && !s.IsArchived, ct))
            throw new ArgumentException("Choisissez un modèle sportif actif de votre foyer.");
    }

    private static ManualSportDto SportDto(ActivitySession s) => new(s.Id, s.DayPlanId, s.SportTemplateId,
        s.Name ?? s.Type, s.Type, s.Intensity, s.DurationMinutes, s.DistanceKm, s.EstimatedEnergyKcal, s.StartMinute, s.EndMinute);

    private static async Task<IResult> DeleteSportAsync(Guid id, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var session = await db.ActivitySessions.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (session is null || !await OwnsDay(db, household.Value, session.DayPlanId, ct)) return Results.NotFound();
        db.ActivitySessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
