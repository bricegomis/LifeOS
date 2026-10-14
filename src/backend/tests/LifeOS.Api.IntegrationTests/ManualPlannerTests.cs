using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Dtos;
using LifeOS.Api.Endpoints;
using LifeOS.Application.FoodItems;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class ManualPlannerTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Post<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<FoodItemDto> Food(HttpClient client, string name, string referenceUnit,
        object? nutrition) =>
        Post<FoodItemDto>(client, "/api/foods", new { name, referenceUnit, nutrition });

    [Fact]
    public async Task Meal_accepts_recipe_and_private_preparation_and_calculates_each_line()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var rice = await Food(client, "Riz", "100g",
            new { caloriesPerUnit = 150, proteinsPerUnit = 3, carbsPerUnit = 30, fatsPerUnit = 1 });
        var coconutMilk = await Food(client, "Lait de coco", "100ml",
            new { caloriesPerUnit = 20, proteinsPerUnit = 0, carbsPerUnit = 2, fatsPerUnit = 1 });
        var chia = await Food(client, "Chia", "100g",
            new { caloriesPerUnit = 500, proteinsPerUnit = 20, carbsPerUnit = 40, fatsPerUnit = 30 });
        var recipe = await Post<RecipeDto>(client, "/api/recipes",
            new { name = "Riz vapeur", servings = 2, durationMinutes = 0 });
        (await client.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients",
            new { foodItemId = rice.Id, quantity = 200, unit = "g" })).EnsureSuccessStatusCode();
        var week = await Post<ManualWeekDto>(client, "/api/manual-planner/weeks",
            new ManualWeekRequest(new(2026, 10, 5)));

        var meal = await Post<ManualMealDto>(client, "/api/manual-planner/meals",
            new ManualMealRequest(week.Days[0].Id, 720, 750,
            [
                new PlannedRecipeLineRequest(recipe.Id, null, null, null, 1),
                new PlannedRecipeLineRequest(null, null, null, 2, 1,
                [
                    new(coconutMilk.Id, 125, "ml"),
                    new(chia.Id, 25, "g")
                ])
            ]));

        Assert.Equal(2, meal.RecipeLines.Count);
        Assert.Equal("Riz vapeur", meal.RecipeLines[0].Name);
        Assert.False(meal.RecipeLines[0].IsAdHoc);
        Assert.True(meal.RecipeLines[0].Nutrition.IsComplete);
        Assert.Equal(150, meal.RecipeLines[0].Nutrition.Calories);
        Assert.Null(meal.RecipeLines[1].Name);
        Assert.True(meal.RecipeLines[1].IsAdHoc);
        Assert.Equal(2, meal.RecipeLines[1].Ingredients.Count);
        Assert.Equal(0.5m, meal.RecipeLines[1].Ingredients[0].RequiredQuantity / 125);
        Assert.Equal(75, meal.RecipeLines[1].Nutrition.Calories);
        Assert.Equal(225, meal.Nutrition.Calories);

        var stored = (await client.GetFromJsonAsync<ManualWeekDto>(
            $"/api/manual-planner/weeks/{week.Id}"))!;
        Assert.Equal(2, Assert.Single(stored.Days[0].Meals).RecipeLines.Count);
    }

    [Fact]
    public async Task Week_duplication_copies_meals_without_sharing_mutable_lines()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Food(client, "Carotte", "piece", null);
        var replacementFood = await Food(client, "Tomate", "piece", null);
        var week = await Post<ManualWeekDto>(client, "/api/manual-planner/weeks",
            new ManualWeekRequest(new(2026, 10, 5)));
        var sourceMeal = await Post<ManualMealDto>(client, "/api/manual-planner/meals",
            new ManualMealRequest(week.Days[0].Id, 720, 750,
            [
                new PlannedRecipeLineRequest(null, null, null, 2, 1,
                    [new(food.Id, 1, "piece")])
            ]));

        var copy = await Post<ManualWeekDto>(client,
            $"/api/manual-planner/weeks/{week.Id}/duplicate",
            new ManualWeekRequest(new(2026, 10, 12)));
        var copiedMeal = Assert.Single(copy.Days[0].Meals);
        Assert.NotEqual(sourceMeal.Id, copiedMeal.Id);
        Assert.NotEqual(sourceMeal.RecipeLines[0].Id, copiedMeal.RecipeLines[0].Id);
        Assert.Equal(sourceMeal.RecipeLines[0].Ingredients[0].Quantity,
            copiedMeal.RecipeLines[0].Ingredients[0].Quantity);
        Assert.Null(copiedMeal.Nutrition.Calories);

        (await client.PutAsJsonAsync($"/api/manual-planner/meals/{sourceMeal.Id}",
            new ManualMealRequest(week.Days[0].Id, 720, 750,
            [
                new PlannedRecipeLineRequest(null, null, null, 2, 1,
                    [new(replacementFood.Id, 2, "piece")])
            ], ReplaceContent: true)))
            .EnsureSuccessStatusCode();
        var persistedCopy = (await client.GetFromJsonAsync<ManualWeekDto>(
            $"/api/manual-planner/weeks/{copy.Id}"))!;
        Assert.Single(Assert.Single(persistedCopy.Days[0].Meals).RecipeLines);
    }

    [Fact]
    public async Task Household_isolation_and_invalid_portion_modes_are_enforced()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Food(a, "Salade", "100g", null);
        var week = await Post<ManualWeekDto>(a, "/api/manual-planner/weeks",
            new ManualWeekRequest(new(2026, 10, 5)));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/manual-planner/meals",
            new ManualMealRequest(week.Days[0].Id, 720, 750,
                [new PlannedRecipeLineRequest(null, null, null, 2, 1,
                    [new(food.Id, 100, "g")],
                    [new PlannedLinePortionRequest(Guid.NewGuid(), 1)])]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(
            $"/api/manual-planner/weeks/{week.Id}")).StatusCode);
    }

    [Fact]
    public async Task Household_reviews_allow_zero_and_group_and_person_targets_and_copy_with_preparation()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var otherHousehold = factory.CreateClient().AsUser(Guid.NewGuid());
        var profile = await Post<System.Text.Json.JsonElement>(client, "/api/households/profiles/",
            new { displayName = "Enfant", portionCoefficient = 0.5m });
        var profileId = profile.GetProperty("id").GetGuid();
        var group = await Post<System.Text.Json.JsonElement>(client, "/api/household-groups/",
            new { name = "Enfants" });
        var groupId = group.GetProperty("id").GetGuid();
        var food = await Food(client, "Mozzarella", "piece", null);

        await Post<ReviewDto>(client, $"/api/reviews/foods/{food.Id}",
            new { memberProfileId = profileId, score = 0, comment = (string?)null });
        await Post<ReviewDto>(client, $"/api/reviews/foods/{food.Id}",
            new { householdGroupId = groupId, score = (int?)null, comment = "Appréciée" });
        var foodReviews = await client.GetFromJsonAsync<List<ReviewDto>>($"/api/reviews/foods/{food.Id}");
        Assert.Equal(2, foodReviews!.Count);
        Assert.Contains(foodReviews, review => review.Score == 0 && review.MemberProfileId == profileId);
        Assert.Contains(foodReviews, review => review.Comment == "Appréciée" && review.HouseholdGroupId == groupId);
        Assert.Equal(HttpStatusCode.NotFound, (await otherHousehold.GetAsync(
            $"/api/reviews/foods/{food.Id}")).StatusCode);

        var recipe = await Post<RecipeDto>(client, "/api/recipes",
            new { name = "Salade", servings = 2, durationMinutes = 0 });
        await Post<ReviewDto>(client, $"/api/reviews/recipes/{recipe.Id}",
            new { memberProfileId = profileId, score = 8, comment = (string?)null });
        var recipeReviews = await client.GetFromJsonAsync<List<ReviewDto>>(
            $"/api/reviews/recipes/{recipe.Id}");
        Assert.Equal(8, Assert.Single(recipeReviews!).Score);

        var week = await Post<ManualWeekDto>(client, "/api/manual-planner/weeks",
            new ManualWeekRequest(new(2026, 10, 5)));
        var meal = await Post<ManualMealDto>(client, "/api/manual-planner/meals",
            new ManualMealRequest(week.Days[0].Id, 720, 750,
            [
                new PlannedRecipeLineRequest(null, "Préparation", null, 2, 1,
                    [new(food.Id, 1, "piece")])
            ]));
        var line = Assert.Single(meal.RecipeLines);
        await Post<ReviewDto>(client, $"/api/reviews/planned-lines/{line.Id}",
            new { memberProfileId = profileId, score = (int?)null, comment = "À refaire" });
        var copy = await Post<ManualWeekDto>(client,
            $"/api/manual-planner/weeks/{week.Id}/duplicate",
            new ManualWeekRequest(new(2026, 10, 12)));
        var copiedLine = Assert.Single(Assert.Single(copy.Days[0].Meals).RecipeLines);
        Assert.NotEqual(line.Id, copiedLine.Id);
        var copiedReviews = await client.GetFromJsonAsync<List<ReviewDto>>(
            $"/api/reviews/planned-lines/{copiedLine.Id}");
        Assert.Equal("À refaire", Assert.Single(copiedReviews!).Comment);
    }
}
