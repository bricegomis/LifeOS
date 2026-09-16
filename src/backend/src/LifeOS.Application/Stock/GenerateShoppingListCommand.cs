using LifeOS.Application.Articles;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.ComposedMeals;
using LifeOS.Application.Recipes;
using LifeOS.Application.WeekPlanning;
using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Stock;

namespace LifeOS.Application.Stock;

/// <summary>
/// Generates a shopping list for a week based on planned meals.
/// Aggregates ingredients by grocery item and deducts available stock.
/// </summary>
public sealed class GenerateShoppingListCommand(
    IWeekRepository weekRepository,
    IDayPlanRepository dayPlanRepository,
    IPlannedMealRepository plannedMealRepository,
    IRecipeRepository recipeRepository,
    IComposedMealRepository composedMealRepository,
    IArticleRepository articleRepository,
    IFoodItemRepository foodItemRepository,
    IStockItemRepository stockItemRepository,
    IShoppingListItemRepository shoppingListItemRepository)
{
    private readonly IWeekRepository _weekRepository = weekRepository;
    private readonly IDayPlanRepository _dayPlanRepository = dayPlanRepository;
    private readonly IPlannedMealRepository _plannedMealRepository = plannedMealRepository;
    private readonly IRecipeRepository _recipeRepository = recipeRepository;
    private readonly IComposedMealRepository _composedMealRepository = composedMealRepository;
    private readonly IArticleRepository _articleRepository = articleRepository;
    private readonly IFoodItemRepository _foodItemRepository = foodItemRepository;
    private readonly IStockItemRepository _stockItemRepository = stockItemRepository;
    private readonly IShoppingListItemRepository _shoppingListItemRepository = shoppingListItemRepository;

    public async Task<List<ShoppingListItemDto>> ExecuteAsync(
        Guid householdId,
        Guid weekId,
        CancellationToken cancellationToken)
    {
        // Get the week to verify it belongs to the household
        var week = await _weekRepository.GetByIdAsync(weekId, householdId, cancellationToken);

        if (week == null)
        {
            throw new ArgumentException("Week not found or does not belong to this household.", nameof(weekId));
        }

        // Clear existing shopping list for this week
        var existingItems = await _shoppingListItemRepository.GetAllForWeekAsync(householdId, weekId, cancellationToken);
        await _shoppingListItemRepository.RemoveRangeAsync(existingItems, cancellationToken);

        // Get all articles (GroceryItems) for this household
        var householdArticles = await _articleRepository.GetAllForHouseholdAsync(householdId, cancellationToken);
        var articlesById = householdArticles.ToDictionary(a => a.Id);
        var articlesByName = householdArticles
            .GroupBy(a => a.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Get all day plans for the week
        var dayPlans = await _dayPlanRepository.GetAllForWeekAsync(weekId, cancellationToken);

        // Aggregate ingredients by grocery item
        var ingredientsByGroceryItem = new Dictionary<Guid, decimal>();

        foreach (var dayPlan in dayPlans)
        {
            var plannedMeals = await _plannedMealRepository.GetAllForDayAsync(dayPlan.Id, cancellationToken);

            foreach (var meal in plannedMeals)
            {
                // Collect ingredients based on the meal type
                var mealIngredients = await GetMealIngredientsAsync(meal, householdId, articlesById, articlesByName, cancellationToken);

                foreach (var (groceryItemId, quantity) in mealIngredients)
                {
                    if (ingredientsByGroceryItem.ContainsKey(groceryItemId))
                    {
                        ingredientsByGroceryItem[groceryItemId] += quantity;
                    }
                    else
                    {
                        ingredientsByGroceryItem[groceryItemId] = quantity;
                    }
                }
            }
        }

        // Get current stock for this household
        var stockItems = await _stockItemRepository.GetAllForHouseholdAsync(householdId, cancellationToken);

        var stockDict = stockItems.GroupBy(i => i.GroceryItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        // Create shopping list items
        var shoppingListItems = new List<ShoppingListItem>();

        foreach (var (groceryItemId, quantityNeeded) in ingredientsByGroceryItem)
        {
            var quantityFromStock = stockDict.ContainsKey(groceryItemId)
                ? Math.Min(stockDict[groceryItemId], quantityNeeded)
                : 0;

            var shoppingItem = ShoppingListItem.Create(
                householdId,
                weekId,
                groceryItemId,
                quantityNeeded,
                quantityFromStock);

            shoppingListItems.Add(shoppingItem);
        }

        await _shoppingListItemRepository.AddRangeAsync(shoppingListItems, cancellationToken);

        return shoppingListItems.Select(item => new ShoppingListItemDto(
            item.Id,
            item.HouseholdId,
            item.WeekId,
            item.GroceryItemId,
            item.QuantityNeeded,
            item.QuantityFromStock,
            item.Checked)).ToList();
    }

    private async Task<List<(Guid groceryItemId, decimal quantity)>> GetMealIngredientsAsync(
        LifeOS.Domain.WeekPlanning.PlannedMeal meal,
        Guid householdId,
        IReadOnlyDictionary<Guid, LifeOS.Domain.Articles.GroceryItem> articlesById,
        IReadOnlyDictionary<string, Guid> articlesByName,
        CancellationToken cancellationToken)
    {
        var ingredients = new List<(Guid groceryItemId, decimal quantity)>();

        if (meal.RecipeId.HasValue)
        {
            // Get ingredients from recipe
            var recipe = await _recipeRepository.GetByIdAsync(meal.RecipeId.Value, householdId, cancellationToken);

            if (recipe != null)
            {
                var quantityFromParts = meal.Parts.Any()
                    ? meal.Parts.Sum(p => p.PortionMultiplier)
                    : 1m;

                foreach (var ingredient in recipe.Ingredients)
                {
                    var groceryItemId = await ResolveGroceryItemIdAsync(ingredient.FoodItemId, householdId, articlesById, articlesByName, cancellationToken);
                    if (groceryItemId.HasValue)
                    {
                        ingredients.Add((groceryItemId.Value, ingredient.Quantity * quantityFromParts));
                    }
                }
            }
        }
        else if (meal.ComposedMealId.HasValue)
        {
            // Get ingredients from all recipes in the composed meal
            var composedMeal = await _composedMealRepository.GetByIdAsync(meal.ComposedMealId.Value, householdId, cancellationToken);

            if (composedMeal != null)
            {
                var quantityFromParts = meal.Parts.Any()
                    ? meal.Parts.Sum(p => p.PortionMultiplier)
                    : 1m;

                foreach (var part in composedMeal.Parts)
                {
                    var recipe = await _recipeRepository.GetByIdAsync(part.RecipeId, householdId, cancellationToken);

                    if (recipe != null)
                    {
                        foreach (var ingredient in recipe.Ingredients)
                        {
                            var groceryItemId = await ResolveGroceryItemIdAsync(ingredient.FoodItemId, householdId, articlesById, articlesByName, cancellationToken);
                            if (groceryItemId.HasValue)
                            {
                                ingredients.Add((
                                    groceryItemId.Value,
                                    ingredient.Quantity * part.QuantityFactor * quantityFromParts));
                            }
                        }
                    }
                }
            }
        }

        return ingredients;
    }

    private async Task<Guid?> ResolveGroceryItemIdAsync(
        Guid foodItemId,
        Guid householdId,
        IReadOnlyDictionary<Guid, LifeOS.Domain.Articles.GroceryItem> articlesById,
        IReadOnlyDictionary<string, Guid> articlesByName,
        CancellationToken cancellationToken)
    {
        // 1. Direct match: the ingredient already references a GroceryItem ID belonging to this household
        if (articlesById.ContainsKey(foodItemId))
        {
            return foodItemId;
        }

        // 2. Lookup via FoodItem aggregate: find corresponding FoodItem and match by name to a GroceryItem in the household
        var foodItem = await _foodItemRepository.GetByIdAsync(foodItemId, cancellationToken);
        if (foodItem != null && foodItem.HouseholdId == householdId)
        {
            var normalizedName = foodItem.Name.Trim().ToLowerInvariant();
            if (articlesByName.TryGetValue(normalizedName, out var matchingGroceryItemId))
            {
                return matchingGroceryItemId;
            }
        }

        // 3. Ingredient cannot be mapped to any GroceryItem for this household; ignore to protect foreign key integrity
        return null;
    }
}
