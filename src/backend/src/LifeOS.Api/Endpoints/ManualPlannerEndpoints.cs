using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Dtos;
using LifeOS.Application.Households;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using LifeOS.Domain.Recipes;
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
        group.MapPost("/weeks/{id:guid}/duplicate", DuplicateWeekAsync).Produces<ManualWeekDto>(201);
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
        return Results.Ok(await db.Weeks.Where(w => w.HouseholdId == household && w.IsManual)
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

    private static async Task<IResult> DuplicateWeekAsync(Guid id, ManualWeekRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var source = await db.Weeks
            .Include(w => w.DayPlans).ThenInclude(d => d.PlannedMeals).ThenInclude(m => m.RecipeLines).ThenInclude(l => l.Ingredients)
            .Include(w => w.DayPlans).ThenInclude(d => d.PlannedMeals).ThenInclude(m => m.RecipeLines).ThenInclude(l => l.Portions)
            .SingleOrDefaultAsync(w => w.Id == id && w.HouseholdId == household && w.IsManual, ct);
        if (source is null) return Results.NotFound();
        if (request.StartsOn.DayOfWeek != DayOfWeek.Monday)
            return Results.Problem("La semaine copiée doit commencer un lundi.", statusCode: 400);
        if (await db.Weeks.AnyAsync(w => w.HouseholdId == household && w.StartsOn == request.StartsOn && w.IsManual, ct))
            return Results.Problem("Une semaine existe déjà à cette date. Choisissez une autre semaine.", statusCode: 409);

        var copy = Week.CreateManual(household.Value, request.StartsOn, request.TimeZoneId);
        db.Weeks.Add(copy);
        var sourceDayIds = source.DayPlans.Select(d => d.Id).ToList();
        var sports = await db.ActivitySessions.Where(s => sourceDayIds.Contains(s.DayPlanId)).ToListAsync(ct);
        var sourceLineIds = source.DayPlans.SelectMany(d => d.PlannedMeals).SelectMany(m => m.RecipeLines)
            .Select(l => l.Id).ToHashSet();
        var lineReviews = await db.RecipeReviews.Where(r => r.HouseholdId == household
            && r.PlannedRecipeLineId != null && sourceLineIds.Contains(r.PlannedRecipeLineId.Value)).ToListAsync(ct);
        var copiedLines = new Dictionary<Guid, Guid>();

        foreach (var sourceDay in source.DayPlans)
        {
            var targetDay = copy.DayPlans.Single(d => d.Date == request.StartsOn.AddDays(sourceDay.Date.DayNumber - source.StartsOn.DayNumber));
            foreach (var sourceMeal in sourceDay.PlannedMeals)
            {
                var meal = PlannedMeal.CreateManual(targetDay.Id, household.Value);
                meal.Schedule(targetDay.Id, sourceMeal.StartMinute, sourceMeal.EndMinute);
                foreach (var sourceLine in sourceMeal.RecipeLines.OrderBy(l => l.Position))
                {
                    var planned = sourceLine.Portions.Count > 0 ? null : sourceLine.PlannedServings;
                    var line = PlannedRecipeLine.Create(meal.Id, household.Value, sourceLine.RecipeId,
                        sourceLine.Position, sourceLine.Name, sourceLine.Instructions, sourceLine.YieldServings, planned);
                    line.SetPortions(planned, sourceLine.Portions.Select(p => PlannedRecipeLinePortion.Create(
                        line.Id, household.Value, p.MemberProfileId, p.Servings)).ToList());
                    foreach (var ingredient in sourceLine.Ingredients)
                        line.Ingredients.Add(PlannedRecipeLineIngredient.Snapshot(line.Id, household.Value,
                            ingredient.FoodItemId, ingredient.Name, ingredient.Quantity, ingredient.Unit,
                            ingredient.ReferenceUnit, new Nutrition(ingredient.Calories, ingredient.Protein,
                                ingredient.Carbs, ingredient.Fat)));
                    meal.RecipeLines.Add(line);
                    copiedLines[sourceLine.Id] = line.Id;
                }
                targetDay.PlannedMeals.Add(meal);
            }

            foreach (var sport in sports.Where(s => s.DayPlanId == sourceDay.Id))
            {
                var clone = ActivitySession.Create(targetDay.Id, sport.Type, sport.Intensity,
                    sport.DurationMinutes, sport.EstimatedEnergyKcal);
                clone.Schedule(targetDay.Id, sport.StartMinute, sport.EndMinute, sport.Name ?? sport.Type,
                    sport.Type, sport.Intensity, sport.DurationMinutes, sport.DistanceKm,
                    sport.EstimatedEnergyKcal, sport.SportTemplateId);
                db.ActivitySessions.Add(clone);
            }
        }
        foreach (var review in lineReviews)
        {
            if (review.PlannedRecipeLineId is { } lineId && copiedLines.TryGetValue(lineId, out var copiedLineId))
                db.RecipeReviews.Add(RecipeReview.Create(household.Value, null, copiedLineId, review.MemberProfileId,
                    review.HouseholdGroupId, review.Score, review.Comment, review.CreatedByUserId));
        }
        await db.SaveChangesAsync(ct);
        var result = await db.Weeks.Include(w => w.DayPlans).ThenInclude(d => d.PlannedMeals)
            .SingleAsync(w => w.Id == copy.Id, ct);
        return Results.Created($"/api/manual-planner/weeks/{copy.Id}", await WeekDto(db, result, ct));
    }

    private static async Task<IResult> GetWeekAsync(Guid id, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var week = await db.Weeks.Include(w => w.DayPlans).ThenInclude(d => d.PlannedMeals).ThenInclude(m => m.RecipeLines)
                .ThenInclude(l => l.Ingredients)
            .Include(w => w.DayPlans).ThenInclude(d => d.PlannedMeals).ThenInclude(m => m.RecipeLines)
                .ThenInclude(l => l.Portions)
            .SingleOrDefaultAsync(w => w.Id == id && w.HouseholdId == household && w.IsManual, ct);
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
            foreach (var meal in day.PlannedMeals)
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
        var meal = PlannedMeal.CreateManual(request.DayPlanId, household.Value);
        meal.Schedule(request.DayPlanId, request.StartMinute, request.EndMinute);
        await SetRecipeLines(db, meal, household.Value, request.RecipeLines, ct);
        db.PlannedMeals.Add(meal);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/manual-planner/meals/{meal.Id}", await MealDto(db, meal, household.Value, ct));
    }

    private static async Task<IResult> UpdateMealAsync(Guid id, ManualMealRequest request, ClaimsPrincipal user,
        ResolveHouseholdForUserQuery households, LifeOSDbContext db, CancellationToken ct)
    {
        var household = await Household(user, households, ct);
        if (household is null) return Results.Unauthorized();
        var meal = await db.PlannedMeals.Include(m => m.RecipeLines).ThenInclude(l => l.Ingredients)
            .Include(m => m.RecipeLines).ThenInclude(l => l.Portions).SingleOrDefaultAsync(m => m.Id == id, ct);
        if (meal is null || !await OwnsDay(db, household.Value, meal.DayPlanId, ct)
            || !await OwnsDay(db, household.Value, request.DayPlanId, ct)) return Results.NotFound();
        meal.Schedule(request.DayPlanId, request.StartMinute, request.EndMinute);
        if (request.ReplaceContent)
        {
            db.PlannedRecipeLines.RemoveRange(meal.RecipeLines);
            await SetRecipeLines(db, meal, household.Value, request.RecipeLines, ct);
        }
        else if (request.RecipeLines.Count != 0)
        {
            return Results.Problem("Les lignes ne peuvent être envoyées sans demander explicitement leur remplacement.", statusCode: 400);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(await MealDto(db, meal, household.Value, ct));
    }

    private static async Task SetRecipeLines(LifeOSDbContext db, PlannedMeal meal, Guid household,
        IReadOnlyList<PlannedRecipeLineRequest> requests, CancellationToken ct)
    {
        if (requests is null || requests.Count is < 1 or > 50)
            throw new ArgumentException("Ajoutez entre 1 et 50 recettes ou préparations à ce repas.");
        for (var position = 0; position < requests.Count; position++)
        {
            var input = requests[position];
            Recipe? recipe = null;
            if (input.RecipeId is { } recipeId)
            {
                recipe = await db.Recipes.Include(r => r.Ingredients)
                    .SingleOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == household && !r.IsArchived, ct);
                if (recipe is null) throw new ArgumentException("Recette indisponible dans votre bibliothèque.");
                if (input.Ingredients is { Count: > 0 } || input.YieldServings is not null || input.Name is not null)
                    throw new ArgumentException("Une ligne de recette utilise les ingrédients et le rendement de la bibliothèque.");
            }
            else if (input.Ingredients is not { Count: > 0 and <= 100 })
            {
                throw new ArgumentException("Une préparation ponctuelle exige entre 1 et 100 ingrédients.");
            }

            var portions = input.Portions ?? [];
            if (portions.Count > 0 && input.PlannedServings is not null)
                throw new ArgumentException("Indiquez un total explicite ou une répartition, pas les deux.");
            if (portions.Select(p => p.MemberProfileId).Distinct().Count() != portions.Count)
                throw new ArgumentException("Un profil ne peut apparaître qu'une fois dans la répartition.");
            var plannedServings = portions.Count > 0 ? null : input.PlannedServings;
            var yieldServings = recipe is null ? input.YieldServings : recipe.Servings;
            if (yieldServings is null)
                throw new ArgumentException("Indiquez le rendement de la préparation.");
            var name = recipe?.Name ?? input.Name;
            var instructions = recipe is null ? input.Instructions : null;
            var line = PlannedRecipeLine.Create(meal.Id, household, recipe?.Id, position, name,
                instructions, yieldServings.Value, plannedServings);
            foreach (var portion in portions)
            {
                var profile = await db.MemberProfiles.AnyAsync(p => p.Id == portion.MemberProfileId && p.HouseholdId == household, ct);
                if (!profile) throw new ArgumentException("La répartition doit utiliser un profil de votre foyer.");
                line.Portions.Add(PlannedRecipeLinePortion.Create(line.Id, household, portion.MemberProfileId, portion.Servings));
            }
            line.SetPortions(plannedServings, line.Portions.ToList());

            var ingredients = recipe is null
                ? input.Ingredients!.Select(i => (i.FoodItemId, i.Quantity, i.Unit)).ToList()
                : recipe.Ingredients.Select(i => (i.FoodItemId, i.Quantity, i.Unit)).ToList();
            foreach (var ingredient in ingredients)
            {
                var food = await db.FoodItems.SingleOrDefaultAsync(f => f.Id == ingredient.FoodItemId
                    && f.HouseholdId == household && !f.IsArchived, ct);
                if (food is null) throw new ArgumentException("Chaque ingrédient doit référencer un aliment actif du foyer.");
                line.Ingredients.Add(PlannedRecipeLineIngredient.Snapshot(line.Id, household, food.Id, food.Name,
                    ingredient.Quantity, ingredient.Unit, food.ReferenceUnit, food.Nutrition));
            }
            meal.RecipeLines.Add(line);
        }
    }

    private static async Task<ManualMealDto> MealDto(LifeOSDbContext db, PlannedMeal meal, Guid household, CancellationToken ct)
    {
        var lines = await db.PlannedRecipeLines.Where(l => l.PlannedMealId == meal.Id && l.HouseholdId == household)
            .Include(l => l.Ingredients).Include(l => l.Portions).OrderBy(l => l.Position).ToListAsync(ct);
        var profileNames = await db.MemberProfiles.Where(p => p.HouseholdId == household)
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        var lineDtos = lines.Select(line => LineDto(line, profileNames)).ToList();
        var name = string.Join(" · ", lineDtos.Select(l => l.Name ?? "Préparation sans nom"));
        return new(meal.Id, meal.DayPlanId, name, meal.StartMinute, meal.EndMinute,
            lineDtos, SumNutrition(lineDtos.Select(l => l.Nutrition).ToList()));
    }

    private static PlannedRecipeLineDto LineDto(PlannedRecipeLine line, IReadOnlyDictionary<Guid, string> profileNames)
    {
        var totalServings = line.TotalServings;
        var ratio = totalServings / line.YieldServings;
        var warnings = new List<string>();
        double? Total(Func<PlannedRecipeLineIngredient, double?> select, string label)
        {
            var values = line.Ingredients.Select(i =>
            {
                var value = select(i);
                var factor = i.NutritionFactor();
                if (value is null || factor is null) warnings.Add($"{i.Name} : {label} inconnu ou unité incompatible.");
                return value * factor * (double)ratio;
            }).ToList();
            return values.Count > 0 && values.All(v => v is not null) ? values.Sum(v => v!.Value) : null;
        }
        var calories = Total(l => l.Calories, "énergie");
        var protein = Total(l => l.Protein, "protéines");
        var carbs = Total(l => l.Carbs, "glucides");
        var fat = Total(l => l.Fat, "lipides");
        var nutrition = new PersonalNutritionDto(calories, protein, carbs, fat,
            warnings.Count == 0 && line.Ingredients.Count > 0, warnings.Distinct().ToList());
        var portions = line.Portions.Select(p => new PlannedLinePortionDto(p.MemberProfileId,
            profileNames.GetValueOrDefault(p.MemberProfileId) ?? "Profil indisponible", p.Servings)).ToList();
        var ingredients = line.Ingredients.Select(i => new PlannedLineIngredientDto(i.Id, i.FoodItemId,
            i.Name, i.Quantity, i.Unit, i.Quantity * ratio, i.ReferenceUnit, i.Calories,
            i.Protein, i.Carbs, i.Fat)).ToList();
        return new(line.Id, line.RecipeId, line.Name, line.Instructions, line.YieldServings,
            totalServings, line.IsAdHoc, portions, ingredients, nutrition);
    }

    private static PersonalNutritionDto SumNutrition(List<PersonalNutritionDto> meals)
    {
        if (meals.Count == 0)
            return new(null, null, null, null, false, ["Aucun repas planifié."]);
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
