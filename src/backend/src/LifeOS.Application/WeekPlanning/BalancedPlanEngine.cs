using System.Globalization;
using System.Text.Json;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.ComposedMeals;
using LifeOS.Application.Recipes;
using LifeOS.Domain.Articles;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Recipes;
using LifeOS.Domain.WeekPlanning;

namespace LifeOS.Application.WeekPlanning;

/// <summary>
/// Computes the single balanced plan of a week from the household's own data.
/// <para>
/// There is no strategy to pick: nutritional balance is treated as a constraint, then the
/// engine rewards a low budget, low waste and enough diversity over the surrounding month.
/// Dimensions the household data cannot support are excluded from the composite score and
/// reported as limitations instead of being faked.
/// </para>
/// </summary>
public sealed class BalancedPlanEngine : IBalancedPlanEngine
{
    // Nutrition dominates: it is the constraint the compromise is built around.
    private const double NutritionWeight = 0.40;
    private const double CostWeight = 0.25;
    private const double WasteWeight = 0.20;
    private const double DiversityWeight = 0.15;

    /// <summary>Relative gap to the target beyond which a nutrition axis scores zero.</summary>
    private const double NutritionTolerance = 0.25;

    /// <summary>Nutrition score below which the week is considered unbalanced.</summary>
    private const double NutritionConstraintThreshold = 0.60;

    /// <summary>Half-width, in days, of the window used to measure diversity over the month.</summary>
    private const int DiversityWindowDays = 14;

    /// <summary>
    /// Web defaults (camelCase) so the stored explanation and the one returned by the API when a
    /// plan is computed have exactly the same shape.
    /// </summary>
    private static readonly JsonSerializerOptions ExplanationSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IWeekRepository _weekRepository;
    private readonly IDayPlanRepository _dayPlanRepository;
    private readonly IPlannedMealRepository _mealRepository;
    private readonly IBalancedWeekPlanRepository _planRepository;
    private readonly IRecipeRepository _recipeRepository;
    private readonly IComposedMealRepository _composedMealRepository;
    private readonly IFoodItemRepository _foodItemRepository;
    private readonly IArticleRepository _articleRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IBalancedPlanDataSource _dataSource;
    private readonly IUnitOfWork _unitOfWork;

    public BalancedPlanEngine(
        IWeekRepository weekRepository,
        IDayPlanRepository dayPlanRepository,
        IPlannedMealRepository mealRepository,
        IBalancedWeekPlanRepository planRepository,
        IRecipeRepository recipeRepository,
        IComposedMealRepository composedMealRepository,
        IFoodItemRepository foodItemRepository,
        IArticleRepository articleRepository,
        IStockItemRepository stockItemRepository,
        IBalancedPlanDataSource dataSource,
        IUnitOfWork unitOfWork)
    {
        _weekRepository = weekRepository ?? throw new ArgumentNullException(nameof(weekRepository));
        _dayPlanRepository = dayPlanRepository ?? throw new ArgumentNullException(nameof(dayPlanRepository));
        _mealRepository = mealRepository ?? throw new ArgumentNullException(nameof(mealRepository));
        _planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
        _composedMealRepository = composedMealRepository ?? throw new ArgumentNullException(nameof(composedMealRepository));
        _foodItemRepository = foodItemRepository ?? throw new ArgumentNullException(nameof(foodItemRepository));
        _articleRepository = articleRepository ?? throw new ArgumentNullException(nameof(articleRepository));
        _stockItemRepository = stockItemRepository ?? throw new ArgumentNullException(nameof(stockItemRepository));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<(BalancedWeekPlan Plan, BalancedPlanExplanation Explanation)> ComputeAsync(
        Guid weekId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household ID is required.", nameof(householdId));
        }

        var week = await _weekRepository.GetByIdAsync(weekId, householdId, cancellationToken)
            ?? throw new InvalidOperationException($"Week {weekId} not found or doesn't belong to household {householdId}.");

        var context = await LoadContextAsync(week, householdId, cancellationToken);
        var explanation = Evaluate(context);

        var explanationJson = JsonDocument.Parse(JsonSerializer.Serialize(explanation, ExplanationSerializerOptions));
        var plan = BalancedWeekPlan.Create(weekId, explanationJson);

        await _planRepository.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (plan, explanation);
    }

    public async Task ApplyAsync(
        Guid weekId,
        Guid planId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        if (weekId == Guid.Empty)
        {
            throw new ArgumentException("Week ID is required.", nameof(weekId));
        }

        if (planId == Guid.Empty)
        {
            throw new ArgumentException("Plan ID is required.", nameof(planId));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household ID is required.", nameof(householdId));
        }

        var week = await _weekRepository.GetByIdAsync(weekId, householdId, cancellationToken)
            ?? throw new InvalidOperationException($"Week {weekId} not found or doesn't belong to household {householdId}.");

        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken)
            ?? throw new InvalidOperationException($"Balanced plan {planId} not found.");

        if (plan.WeekId != week.Id)
        {
            throw new InvalidOperationException($"Balanced plan {planId} doesn't belong to week {weekId}.");
        }

        // A week retains at most one computed plan.
        var existingPlans = await _planRepository.GetAllForWeekAsync(weekId, cancellationToken);
        foreach (var other in existingPlans.Where(candidate => candidate.Applied && candidate.Id != plan.Id))
        {
            other.Revert();
            await _planRepository.UpdateAsync(other, cancellationToken);
        }

        plan.Apply();
        await _planRepository.UpdateAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- loading

    private async Task<WeekEvaluationContext> LoadContextAsync(
        Week week,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var dayPlans = await _dayPlanRepository.GetAllForWeekAsync(week.Id, householdId, cancellationToken);
        var recipes = (await _recipeRepository.GetAllForHouseholdAsync(householdId, cancellationToken))
            .ToDictionary(recipe => recipe.Id);
        var composedMeals = (await _composedMealRepository.GetAllForHouseholdAsync(householdId, cancellationToken))
            .ToDictionary(meal => meal.Id);
        var foodItemList = (await _foodItemRepository.GetByHouseholdAsync(householdId, cancellationToken)).ToList();
        var foodItems = foodItemList.ToDictionary(item => item.Id);
        var foodItemsByName = foodItemList
            .GroupBy(item => item.Name.Trim().ToLowerInvariant())
            .ToDictionary(group => group.Key, group => group.First());
        var articles = await _articleRepository.GetAllForHouseholdAsync(householdId, cancellationToken);
        var stockItems = await _stockItemRepository.GetAllForHouseholdAsync(householdId, cancellationToken);
        var activityEnergy = await _dataSource.GetActivityEnergyByDayPlanAsync(week.Id, cancellationToken);
        var targets = await _dataSource.GetNutritionTargetsAsync(householdId, cancellationToken);

        var meals = new List<PlannedMealUsage>();
        var plannedDayCount = 0;
        decimal weekActivityEnergy = 0;

        foreach (var dayPlan in dayPlans)
        {
            var dayMeals = await _mealRepository.GetAllForDayAsync(dayPlan.Id, householdId, cancellationToken);
            var kept = dayMeals.Where(meal => meal.Status != "skipped").ToList();

            if (kept.Count > 0)
            {
                plannedDayCount++;
                weekActivityEnergy += activityEnergy.TryGetValue(dayPlan.Id, out var energy) ? energy : 0m;
            }

            foreach (var meal in kept)
            {
                var referenceId = meal.RecipeId ?? meal.ComposedMealId;
                if (referenceId is null)
                {
                    continue;
                }

                var portions = meal.Parts.Count > 0 ? meal.Parts.Sum(part => part.PortionMultiplier) : 1m;
                meals.Add(new PlannedMealUsage(meal.Id, dayPlan.Date, referenceId.Value, portions));
            }
        }

        var windowStart = week.StartsOn.AddDays(-DiversityWindowDays);
        var windowEnd = week.StartsOn.AddDays(6 + DiversityWindowDays);
        var monthReferences = await _dataSource.GetPlannedMealReferencesAsync(
            householdId,
            windowStart,
            windowEnd,
            cancellationToken);

        return new WeekEvaluationContext(
            meals,
            plannedDayCount,
            weekActivityEnergy,
            recipes,
            composedMeals,
            foodItems,
            foodItemsByName,
            articles,
            stockItems,
            monthReferences,
            targets);
    }

    // ------------------------------------------------------------- evaluation

    private static BalancedPlanExplanation Evaluate(WeekEvaluationContext context)
    {
        if (context.Meals.Count == 0)
        {
            return new BalancedPlanExplanation(
                BalancedWeekPlan.BalancedMethod,
                OverallScore: null,
                NutritionConstraintMet: false,
                [
                    EmptyDimension("nutrition", "Aucun repas planifié : équilibre nutritionnel non évaluable."),
                    EmptyDimension("cost", "Aucun repas planifié : budget non évaluable."),
                    EmptyDimension("diversity", "Aucun repas planifié : diversité non évaluable."),
                    EmptyDimension("waste", "Aucun repas planifié : gaspillage non évaluable."),
                ],
                Tradeoffs: [],
                Limitations: ["Aucun repas n'est planifié sur cette semaine : ajoutez des repas pour obtenir un calcul."],
                TextExplanation: "Le calcul équilibré a besoin d'au moins un repas planifié sur la semaine.");
        }

        var limitations = new List<string>();
        var tradeoffs = new List<string>();

        var ingredients = ResolveIngredients(context, limitations);
        var prices = BuildPriceIndex(context);

        var dimensions = new List<BalancedPlanDimension>
        {
            ScoreNutrition(context, ingredients, limitations),
            ScoreCost(context, ingredients, prices, limitations),
            ScoreDiversity(context, limitations),
            ScoreWaste(context, ingredients, prices, limitations),
        };

        var (overall, weighted) = Combine(dimensions);
        var nutrition = weighted[0];
        var nutritionConstraintMet = nutrition.Score is null || nutrition.Score >= NutritionConstraintThreshold;

        if (!nutritionConstraintMet && overall is not null)
        {
            // Nutrition is a constraint, not one criterion among others: a cheap week that is
            // nutritionally off must never be presented as a good compromise.
            overall = Math.Min(overall.Value, nutrition.Score!.Value);
            tradeoffs.Add(
                "L'équilibre nutritionnel est prioritaire : il sort de la plage visée, " +
                "le score global est donc plafonné par celui de la nutrition.");
        }

        tradeoffs.AddRange(DescribeTradeoffs(weighted));

        return new BalancedPlanExplanation(
            BalancedWeekPlan.BalancedMethod,
            overall,
            nutritionConstraintMet,
            weighted,
            tradeoffs,
            limitations,
            BuildText(overall, nutritionConstraintMet, weighted, limitations));
    }

    private static IReadOnlyList<IngredientLine> ResolveIngredients(
        WeekEvaluationContext context,
        List<string> limitations)
    {
        var lines = new List<IngredientLine>();
        var unresolvedMeals = 0;

        foreach (var meal in context.Meals)
        {
            var resolved = false;

            if (context.Recipes.TryGetValue(meal.ReferenceId, out var recipe))
            {
                resolved = recipe.Ingredients.Count > 0;
                AppendRecipe(lines, recipe, meal, meal.Portions);
            }
            else if (context.ComposedMeals.TryGetValue(meal.ReferenceId, out var composedMeal))
            {
                foreach (var part in composedMeal.Parts)
                {
                    if (!context.Recipes.TryGetValue(part.RecipeId, out var partRecipe))
                    {
                        continue;
                    }

                    resolved |= partRecipe.Ingredients.Count > 0;
                    AppendRecipe(lines, partRecipe, meal, meal.Portions * part.QuantityFactor);
                }
            }

            if (!resolved)
            {
                unresolvedMeals++;
            }
        }

        if (unresolvedMeals > 0)
        {
            limitations.Add(
                $"{unresolvedMeals} repas sur {context.Meals.Count} n'ont aucun ingrédient exploitable " +
                "(recette absente ou sans ingrédients) : ils ne comptent ni dans la nutrition, ni dans le budget, ni dans le gaspillage.");
        }

        return lines;

        static void AppendRecipe(List<IngredientLine> target, Recipe recipe, PlannedMealUsage meal, decimal factor)
        {
            // Recipe quantities describe the whole recipe for its declared number of servings.
            var servings = recipe.Servings > 0 ? recipe.Servings : 1;

            foreach (var ingredient in recipe.Ingredients)
            {
                target.Add(new IngredientLine(
                    meal.MealId,
                    ingredient.FoodItemId,
                    ingredient.Quantity * factor / servings));
            }
        }
    }

    private static BalancedPlanDimension ScoreNutrition(
        WeekEvaluationContext context,
        IReadOnlyList<IngredientLine> ingredients,
        List<string> limitations)
    {
        if (context.Targets is null)
        {
            limitations.Add(
                "Aucune cible nutritionnelle n'est configurée : l'équilibre nutritionnel n'a pas pu être vérifié. " +
                "Renseignez-la dans les réglages de nutrition.");
            return EmptyDimension("nutrition", "Cibles nutritionnelles non configurées.");
        }

        if (ingredients.Count == 0)
        {
            return EmptyDimension("nutrition", "Aucun ingrédient exploitable sur la semaine.");
        }

        double energy = 0, proteins = 0, carbs = 0, fats = 0;
        var covered = 0;

        foreach (var line in ingredients)
        {
            var foodItem = ResolveFoodItem(context, line.FoodItemId);

            if (foodItem?.Nutrition is null)
            {
                continue;
            }

            covered++;
            var quantity = (double)line.Quantity;
            energy += (foodItem.Nutrition.CaloriesPerUnit ?? 0) * quantity;
            proteins += (foodItem.Nutrition.ProteinsPerUnit ?? 0) * quantity;
            carbs += (foodItem.Nutrition.CarbsPerUnit ?? 0) * quantity;
            fats += (foodItem.Nutrition.FatsPerUnit ?? 0) * quantity;
        }

        if (covered == 0)
        {
            limitations.Add(
                "Aucun ingrédient de la semaine ne porte de valeurs nutritionnelles : l'équilibre n'a pas pu être vérifié.");
            return EmptyDimension("nutrition", "Aucune donnée nutritionnelle sur les ingrédients.");
        }

        var coverage = (double)covered / ingredients.Count;

        limitations.Add(
            "Les quantités sont additionnées sans conversion d'unité entre recettes et articles : " +
            "budget et apports sont des ordres de grandeur, pas des montants exacts.");

        if (coverage < 0.8)
        {
            limitations.Add(
                $"Seuls {Percent(coverage)} des ingrédients portent des valeurs nutritionnelles : " +
                "l'équilibre affiché est une estimation basse.");
        }

        var days = Math.Max(context.PlannedDayCount, 1);
        var targets = context.Targets;
        var dailyEnergyTarget = (double)(targets.DailyBaseEnergyKcal - targets.TargetNetDeficitKcal)
            + ((double)context.WeekActivityEnergyKcal / days);

        var energyTarget = dailyEnergyTarget * days;
        var proteinTarget = (double)targets.TargetProteinG * days;
        var carbsTarget = (double)targets.TargetCarbsG * days;
        var fatsTarget = (double)targets.TargetFatsG * days;

        var score = (0.40 * AxisScore(energy, energyTarget))
            + (0.30 * AxisScore(proteins, proteinTarget))
            + (0.15 * AxisScore(carbs, carbsTarget))
            + (0.15 * AxisScore(fats, fatsTarget));

        // An incomplete nutrition dataset cannot certify a balanced week.
        score *= coverage;

        var metrics = new Dictionary<string, double>
        {
            ["energyKcal"] = Round(energy),
            ["energyTargetKcal"] = Round(energyTarget),
            ["proteinG"] = Round(proteins),
            ["proteinTargetG"] = Round(proteinTarget),
            ["carbsG"] = Round(carbs),
            ["carbsTargetG"] = Round(carbsTarget),
            ["fatsG"] = Round(fats),
            ["fatsTargetG"] = Round(fatsTarget),
            ["plannedDays"] = days,
        };

        var summary =
            $"{Num(energy)} kcal sur la semaine pour une cible de {Num(energyTarget)} kcal, " +
            $"protéines {Num(proteins)} g sur {Num(proteinTarget)} g.";

        return new BalancedPlanDimension("nutrition", Clamp(score), NutritionWeight, Round(coverage), summary, metrics);
    }

    private static BalancedPlanDimension ScoreCost(
        WeekEvaluationContext context,
        IReadOnlyList<IngredientLine> ingredients,
        PriceIndex prices,
        List<string> limitations)
    {
        if (ingredients.Count == 0)
        {
            return EmptyDimension("cost", "Aucun ingrédient exploitable sur la semaine.");
        }

        decimal total = 0;
        var covered = 0;

        foreach (var line in ingredients)
        {
            var price = ResolveUnitPrice(context, prices, line.FoodItemId);
            if (price is null)
            {
                continue;
            }

            covered++;
            total += line.Quantity * price.Value;
        }

        if (covered == 0)
        {
            limitations.Add(
                "Aucun ingrédient de la semaine n'est rattaché à un article avec un prix relevé : " +
                "le budget n'a pas pu être estimé.");
            return EmptyDimension("cost", "Aucun prix disponible pour les ingrédients de la semaine.");
        }

        var coverage = (double)covered / ingredients.Count;

        limitations.Add(
            "Les quantités sont additionnées sans conversion d'unité entre recettes et articles : " +
            "budget et apports sont des ordres de grandeur, pas des montants exacts.");

        if (coverage < 0.8)
        {
            limitations.Add(
                $"Seuls {Percent(coverage)} des ingrédients ont un prix relevé : " +
                "le budget estimé est un minorant, pas le coût réel des courses.");
        }

        var actual = (double)total;

        // The budget is normalised against what the household library itself makes possible:
        // the cheapest and the most expensive way to fill the same number of meals.
        var referenceCosts = BuildReferenceMealCosts(context, prices);

        if (referenceCosts.Count < 2)
        {
            limitations.Add(
                "Moins de deux recettes entièrement chiffrables dans la bibliothèque : le budget est affiché " +
                "mais ne peut pas être comparé à une alternative moins chère.");

            return new BalancedPlanDimension(
                "cost",
                Score: null,
                Weight: 0,
                Round(coverage),
                $"Budget estimé à {Money(actual)} € sur la semaine, sans comparaison possible.",
                new Dictionary<string, double> { ["estimatedCostEur"] = Round(actual) });
        }

        var mealCount = context.Meals.Count;
        var cheapest = (double)referenceCosts.Min() * mealCount;
        var priciest = (double)referenceCosts.Max() * mealCount;

        var score = priciest > cheapest ? (priciest - actual) / (priciest - cheapest) : 1.0;

        var metrics = new Dictionary<string, double>
        {
            ["estimatedCostEur"] = Round(actual),
            ["cheapestReachableCostEur"] = Round(cheapest),
            ["mostExpensiveReachableCostEur"] = Round(priciest),
            ["mealCount"] = mealCount,
        };

        var summary =
            $"Budget estimé à {Money(actual)} € ; le plan le moins cher possible avec votre bibliothèque " +
            $"coûterait {Money(cheapest)} €.";

        return new BalancedPlanDimension("cost", Clamp(score), CostWeight, Round(coverage), summary, metrics);
    }

    private static BalancedPlanDimension ScoreDiversity(WeekEvaluationContext context, List<string> limitations)
    {
        var references = context.MonthReferences;

        if (references.Count == 0)
        {
            // Fall back on the week itself when no history surrounds it.
            references = context.Meals
                .Select(meal => new PlannedMealReference(meal.Date, meal.ReferenceId))
                .ToList();
            limitations.Add(
                "Aucun repas planifié autour de cette semaine : la diversité est mesurée sur la semaine seule, " +
                "pas sur le mois.");
        }
        else if (references.Count <= context.Meals.Count)
        {
            limitations.Add(
                "Peu d'historique autour de cette semaine : la diversité sur le mois reste indicative.");
        }

        var counts = references
            .GroupBy(reference => reference.ReferenceId)
            .Select(group => group.Count())
            .ToList();

        var distinctRate = (double)counts.Count / references.Count;
        var topShare = (double)counts.Max() / references.Count;
        var score = (0.7 * distinctRate) + (0.3 * (1 - topShare));

        var metrics = new Dictionary<string, double>
        {
            ["mealsInWindow"] = references.Count,
            ["distinctMeals"] = counts.Count,
            ["mostRepeatedMealShare"] = Round(topShare),
            ["windowDays"] = (DiversityWindowDays * 2) + 7,
        };

        var summary =
            $"{counts.Count} repas différents sur {references.Count} planifiés autour de cette semaine ; " +
            $"le repas le plus fréquent représente {Percent(topShare)} du total.";

        return new BalancedPlanDimension("diversity", Clamp(score), DiversityWeight, 1.0, summary, metrics);
    }

    private static BalancedPlanDimension ScoreWaste(
        WeekEvaluationContext context,
        IReadOnlyList<IngredientLine> ingredients,
        PriceIndex prices,
        List<string> limitations)
    {
        if (ingredients.Count == 0)
        {
            return EmptyDimension("waste", "Aucun ingrédient exploitable sur la semaine.");
        }

        // Proxy 1: quantities already in stock are consumed instead of being bought again.
        var stockByGroceryItem = context.StockItems
            .GroupBy(item => item.GroceryItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var neededByGroceryItem = new Dictionary<Guid, decimal>();

        foreach (var line in ingredients)
        {
            var groceryItemId = ResolveGroceryItemId(context, prices, line.FoodItemId);
            if (groceryItemId is null)
            {
                continue;
            }

            neededByGroceryItem[groceryItemId.Value] =
                neededByGroceryItem.GetValueOrDefault(groceryItemId.Value) + line.Quantity;
        }

        decimal needed = 0;
        decimal fromStock = 0;

        foreach (var (groceryItemId, quantity) in neededByGroceryItem)
        {
            needed += quantity;
            if (stockByGroceryItem.TryGetValue(groceryItemId, out var available))
            {
                fromStock += Math.Min(available, quantity);
            }
        }

        var stockCoverage = needed > 0 ? (double)(fromStock / needed) : 0;

        // Proxy 2: an ingredient used by a single meal is the one most likely to leave a
        // leftover, because the rest of the pack is not planned anywhere else in the week.
        var mealsByFoodItem = ingredients
            .GroupBy(line => line.FoodItemId)
            .ToDictionary(group => group.Key, group => group.Select(line => line.MealId).Distinct().Count());

        var reusedCount = mealsByFoodItem.Count(entry => entry.Value >= 2);
        var reuseRate = mealsByFoodItem.Count > 0 ? (double)reusedCount / mealsByFoodItem.Count : 0;
        var singleUseCount = mealsByFoodItem.Count - reusedCount;

        double score;
        double dataCoverage;

        if (neededByGroceryItem.Count == 0)
        {
            limitations.Add(
                "Aucun ingrédient n'est rattaché à un article de course : la part déjà en stock n'a pas pu être mesurée, " +
                "le score anti-gaspillage ne repose que sur la réutilisation des ingrédients entre repas.");
            score = reuseRate;
            dataCoverage = 0.5;
        }
        else
        {
            score = (0.5 * stockCoverage) + (0.5 * reuseRate);
            dataCoverage = 1.0;
        }

        limitations.Add(
            "Le gaspillage est estimé indirectement (stock déjà disponible et ingrédients réutilisés entre plusieurs repas) : " +
            "sans dates de péremption ni tailles de conditionnement, aucune absence de reste ne peut être garantie.");

        var metrics = new Dictionary<string, double>
        {
            ["stockCoverage"] = Round(stockCoverage),
            ["reusedIngredientRate"] = Round(reuseRate),
            ["singleUseIngredients"] = singleUseCount,
            ["distinctIngredients"] = mealsByFoodItem.Count,
        };

        var summary =
            $"{reusedCount} ingrédients sur {mealsByFoodItem.Count} servent à au moins deux repas et " +
            $"{Percent(stockCoverage)} des quantités sont déjà en stock ; {singleUseCount} ingrédients restent à usage unique.";

        return new BalancedPlanDimension("waste", Clamp(score), WasteWeight, Round(dataCoverage), summary, metrics);
    }

    // ------------------------------------------------------------ composition

    private static (double? Overall, IReadOnlyList<BalancedPlanDimension> Weighted) Combine(
        IReadOnlyList<BalancedPlanDimension> dimensions)
    {
        var totalWeight = dimensions
            .Where(dimension => dimension.Score is not null)
            .Sum(dimension => DefaultWeight(dimension.Key));

        if (totalWeight <= 0)
        {
            return (null, dimensions.Select(dimension => dimension with { Weight = 0 }).ToList());
        }

        var weighted = new List<BalancedPlanDimension>(dimensions.Count);
        double overall = 0;

        foreach (var dimension in dimensions)
        {
            if (dimension.Score is null)
            {
                weighted.Add(dimension with { Weight = 0 });
                continue;
            }

            var weight = DefaultWeight(dimension.Key) / totalWeight;
            overall += weight * dimension.Score.Value;
            weighted.Add(dimension with { Weight = Round(weight) });
        }

        return (Round(overall), weighted);
    }

    private static double DefaultWeight(string key) => key switch
    {
        "nutrition" => NutritionWeight,
        "cost" => CostWeight,
        "waste" => WasteWeight,
        "diversity" => DiversityWeight,
        _ => 0,
    };

    private static IEnumerable<string> DescribeTradeoffs(IReadOnlyList<BalancedPlanDimension> dimensions)
    {
        var scored = dimensions.Where(dimension => dimension.Score is not null).ToList();

        if (scored.Count < 2)
        {
            yield break;
        }

        var best = scored.MaxBy(dimension => dimension.Score)!;
        var worst = scored.MinBy(dimension => dimension.Score)!;

        if (best.Key != worst.Key && best.Score - worst.Score >= 0.2)
        {
            yield return $"Compromis retenu : {LabelOf(best.Key)} est privilégié, {LabelOf(worst.Key)} est le point faible de la semaine.";
        }

        foreach (var dimension in scored.Where(dimension => dimension.Score < 0.5))
        {
            yield return $"{LabelOf(dimension.Key)} : {dimension.Summary}";
        }
    }

    private static string BuildText(
        double? overall,
        bool nutritionConstraintMet,
        IReadOnlyList<BalancedPlanDimension> dimensions,
        IReadOnlyList<string> limitations)
    {
        if (overall is null)
        {
            return "Les données de la semaine ne permettent aucun calcul : complétez la bibliothèque, les prix ou les cibles nutritionnelles.";
        }

        var scored = dimensions.Where(dimension => dimension.Score is not null).ToList();
        var detail = string.Join(
            ", ",
            scored.Select(dimension => $"{LabelOf(dimension.Key)} {Percent(dimension.Score!.Value)}"));

        var constraint = nutritionConstraintMet
            ? "L'équilibre nutritionnel reste dans la plage visée."
            : "L'équilibre nutritionnel sort de la plage visée et plafonne le score global.";

        var skipped = dimensions.Count - scored.Count;
        var missing = skipped > 0
            ? $" {skipped} dimension(s) n'ont pas pu être calculées faute de données."
            : string.Empty;

        var caveat = limitations.Count > 0 ? " Les limites du calcul sont listées avec le résultat." : string.Empty;

        return $"Compromis calculé à {Percent(overall.Value)} ({detail}). {constraint}{missing}{caveat}";
    }

    // ----------------------------------------------------------------- prices

    private static PriceIndex BuildPriceIndex(WeekEvaluationContext context)
    {
        var byId = context.Articles.ToDictionary(article => article.Id);
        var byName = context.Articles
            .GroupBy(article => Normalize(article.Name))
            .ToDictionary(group => group.Key, group => group.First().Id);

        return new PriceIndex(byId, byName);
    }

    /// <summary>
    /// Mirrors the shopping list resolution: an ingredient either already references a grocery
    /// item of the household, or is matched to one by food item name.
    /// </summary>
    private static Guid? ResolveGroceryItemId(WeekEvaluationContext context, PriceIndex prices, Guid foodItemId)
    {
        if (prices.ById.ContainsKey(foodItemId))
        {
            return foodItemId;
        }

        if (context.FoodItems.TryGetValue(foodItemId, out var foodItem)
            && prices.ByName.TryGetValue(Normalize(foodItem.Name), out var articleId))
        {
            return articleId;
        }

        return null;
    }

    /// <summary>
    /// Recipe ingredients reference a grocery item of the household. Nutrition lives on food
    /// items, which are joined by name, so an ingredient is matched either directly to a food
    /// item or through the name of its grocery item.
    /// </summary>
    private static FoodItem? ResolveFoodItem(WeekEvaluationContext context, Guid ingredientReferenceId)
    {
        if (context.FoodItems.TryGetValue(ingredientReferenceId, out var direct))
        {
            return direct;
        }

        var article = context.Articles.FirstOrDefault(candidate => candidate.Id == ingredientReferenceId);

        if (article is not null && context.FoodItemsByName.TryGetValue(Normalize(article.Name), out var byName))
        {
            return byName;
        }

        return null;
    }

    private static decimal? ResolveUnitPrice(WeekEvaluationContext context, PriceIndex prices, Guid foodItemId)
    {
        var groceryItemId = ResolveGroceryItemId(context, prices, foodItemId);

        if (groceryItemId is null || !prices.ById.TryGetValue(groceryItemId.Value, out var article))
        {
            return null;
        }

        return LatestPrice(article);
    }

    private static decimal? LatestPrice(GroceryItem article)
    {
        if (article.PriceHistory.Count == 0)
        {
            return null;
        }

        return article.PriceHistory
            .OrderByDescending(entry => entry.ObservedAt)
            .First()
            .Price;
    }

    /// <summary>
    /// Cost of one serving of every fully priceable recipe of the library, used as the reference
    /// range the week's budget is compared against.
    /// </summary>
    private static List<decimal> BuildReferenceMealCosts(WeekEvaluationContext context, PriceIndex prices)
    {
        var costs = new List<decimal>();

        foreach (var recipe in context.Recipes.Values)
        {
            if (recipe.Ingredients.Count == 0)
            {
                continue;
            }

            decimal cost = 0;
            var complete = true;

            foreach (var ingredient in recipe.Ingredients)
            {
                var price = ResolveUnitPrice(context, prices, ingredient.FoodItemId);
                if (price is null)
                {
                    complete = false;
                    break;
                }

                cost += ingredient.Quantity * price.Value;
            }

            if (complete)
            {
                var servings = recipe.Servings > 0 ? recipe.Servings : 1;
                costs.Add(cost / servings);
            }
        }

        return costs;
    }

    // ---------------------------------------------------------------- helpers

    private static BalancedPlanDimension EmptyDimension(string key, string summary) =>
        new(key, Score: null, Weight: 0, DataCoverage: 0, summary, new Dictionary<string, double>());

    private static double AxisScore(double actual, double target)
    {
        if (target <= 0)
        {
            return 0;
        }

        var deviation = Math.Abs(actual - target) / target;
        return Clamp(1 - (deviation / NutritionTolerance));
    }

    private static double Clamp(double value) => Math.Round(Math.Clamp(value, 0, 1), 4);

    private static double Round(double value) => Math.Round(value, 4);

    private static string Percent(double ratio) =>
        Math.Round(ratio * 100).ToString("0", CultureInfo.InvariantCulture) + " %";

    private static string Num(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static string Money(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static string LabelOf(string key) => key switch
    {
        "nutrition" => "équilibre nutritionnel",
        "cost" => "budget",
        "diversity" => "diversité",
        "waste" => "anti-gaspillage",
        _ => key,
    };

    private sealed record PlannedMealUsage(Guid MealId, DateOnly Date, Guid ReferenceId, decimal Portions);

    private sealed record IngredientLine(Guid MealId, Guid FoodItemId, decimal Quantity);

    private sealed record PriceIndex(
        IReadOnlyDictionary<Guid, GroceryItem> ById,
        IReadOnlyDictionary<string, Guid> ByName);

    private sealed record WeekEvaluationContext(
        IReadOnlyList<PlannedMealUsage> Meals,
        int PlannedDayCount,
        decimal WeekActivityEnergyKcal,
        IReadOnlyDictionary<Guid, Recipe> Recipes,
        IReadOnlyDictionary<Guid, Domain.ComposedMeals.ComposedMeal> ComposedMeals,
        IReadOnlyDictionary<Guid, FoodItem> FoodItems,
        IReadOnlyDictionary<string, FoodItem> FoodItemsByName,
        IReadOnlyList<GroceryItem> Articles,
        IReadOnlyList<Domain.Stock.StockItem> StockItems,
        IReadOnlyList<PlannedMealReference> MonthReferences,
        NutritionTargets? Targets);
}
