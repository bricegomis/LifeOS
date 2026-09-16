using LifeOS.Application.ComposedMeals;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Application.Recipes;
using LifeOS.Application.WeekPlanning;
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
    IStockItemRepository stockItemRepository,
    IShoppingListItemRepository shoppingListItemRepository)
{
    private readonly IWeekRepository _weekRepository = weekRepository;
    private readonly IDayPlanRepository _dayPlanRepository = dayPlanRepository;
    private readonly IPlannedMealRepository _plannedMealRepository = plannedMealRepository;
    private readonly IRecipeRepository _recipeRepository = recipeRepository;
    private readonly IComposedMealRepository _composedMealRepository = composedMealRepository;
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
                var mealIngredients = await GetMealIngredientsAsync(meal, householdId, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var ingredients = new List<(Guid groceryItemId, decimal quantity)>();

        if (meal.RecipeId.HasValue)
        {
            // Get ingredients from recipe
            var recipe = await _recipeRepository.GetByIdAsync(meal.RecipeId.Value, householdId, cancellationToken);

            if (recipe != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    // For now, use FoodItemId as GroceryItemId
                    // In a real scenario, we would map FoodItem to GroceryItem
                    var quantityFromParts = meal.Parts.Any()
                        ? meal.Parts.Sum(p => p.PortionMultiplier)
                        : 1m;

                    ingredients.Add((ingredient.FoodItemId, ingredient.Quantity * quantityFromParts));
                }
            }
        }
        else if (meal.ComposedMealId.HasValue)
        {
            // Get ingredients from all recipes in the composed meal
            var composedMeal = await _composedMealRepository.GetByIdAsync(meal.ComposedMealId.Value, householdId, cancellationToken);

            if (composedMeal != null)
            {
                foreach (var part in composedMeal.Parts)
                {
                    var recipe = await _recipeRepository.GetByIdAsync(part.RecipeId, householdId, cancellationToken);

                    if (recipe != null)
                    {
                        foreach (var ingredient in recipe.Ingredients)
                        {
                            var quantityFromParts = meal.Parts.Any()
                                ? meal.Parts.Sum(p => p.PortionMultiplier)
                                : 1m;

                            ingredients.Add((
                                ingredient.FoodItemId,
                                ingredient.Quantity * part.QuantityFactor * quantityFromParts));
                        }
                    }
                }
            }
        }

        return ingredients;
    }
}
