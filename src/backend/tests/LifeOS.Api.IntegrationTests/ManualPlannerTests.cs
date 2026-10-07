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

    private static Task<FoodItemDto> Food(HttpClient client, string name = "Riz") =>
        Post<FoodItemDto>(client, "/api/food-items", new
        {
            name, referenceUnit = "100g",
            nutrition = new { caloriesPerUnit = 150, proteinsPerUnit = 3, carbsPerUnit = 30, fatsPerUnit = 1 }
        });

    [Fact]
    public async Task Explicit_replacement_quantity_edits_and_sport_catalog_isolation()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Food(a);
        var replacement = await Food(a, "Autre riz");
        var week = await Post<ManualWeekDto>(a, "/api/manual-planner/weeks", new ManualWeekRequest(new(2026, 10, 5)));
        var foreignWeek = await Post<ManualWeekDto>(b, "/api/manual-planner/weeks", new ManualWeekRequest(new(2026, 10, 5)));
        var day = week.Days[0].Id;
        var meal = await Post<ManualMealDto>(a, "/api/manual-planner/meals",
            new ManualMealRequest(day, 480, 500, 1, 0, Lines: [new(food.Id, 100, "g")]));
        var response = await a.PutAsJsonAsync($"/api/manual-planner/meals/{meal.Id}",
            new ManualMealRequest(day, 480, 500, 1, 2, Lines: [new(food.Id, 50, "g", meal.Lines[0].Id)]));
        response.EnsureSuccessStatusCode();
        var adjusted = (await response.Content.ReadFromJsonAsync<ManualMealDto>())!;
        Assert.Equal(75d, adjusted.Nutrition.Calories);
        Assert.Equal(100, adjusted.Lines[0].PreparationQuantity);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync($"/api/manual-planner/meals/{meal.Id}",
            new ManualMealRequest(day, 480, 500, 1, 0, Lines: [new(replacement.Id, 100, "g")]))).StatusCode);
        (await a.PutAsJsonAsync($"/api/manual-planner/meals/{meal.Id}",
            new ManualMealRequest(day, 480, 500, 1, 0, Lines: [new(replacement.Id, 100, "g")], ReplaceContent: true)))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/api/manual-planner/meals",
            new ManualMealRequest(foreignWeek.Days[0].Id, 480, 500, 1, 0, Lines: [new(food.Id, 100, "g")]))).StatusCode);
        var templateRequest = new SportTemplatesEndpoints.SportTemplateRequest("Vélo", "bike", 30, 10, "moderate", 200);
        var template = await Post<System.Text.Json.JsonElement>(a, "/api/sport-templates", templateRequest);
        var id = template.GetProperty("id").GetGuid();
        var sport = await Post<ManualSportDto>(a, "/api/manual-planner/sports",
            new ManualSportRequest(day, 1020, 1050, id, "Vélo", "bike", "moderate", 30, 10, 200));
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/sport-templates/{id}", templateRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/sport-templates/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsJsonAsync("/api/manual-planner/sports",
            new ManualSportRequest(foreignWeek.Days[0].Id, 1020, 1050, id, "Vélo", "bike", "moderate", 30, 10, 200))).StatusCode);
        (await a.PutAsJsonAsync($"/api/sport-templates/{id}", templateRequest with { Calories = 999 })).EnsureSuccessStatusCode();
        (await a.DeleteAsync($"/api/sport-templates/{id}")).EnsureSuccessStatusCode();
        (await a.PutAsJsonAsync($"/api/manual-planner/sports/{sport.Id}",
            new ManualSportRequest(week.Days[1].Id, 1320, 1380, id, "Vélo", "bike", "moderate", 60, 20, 200)))
            .EnsureSuccessStatusCode();
        var read = (await a.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{week.Id}"))!;
        Assert.Equal("Autre riz", Assert.Single(read.Days[0].Meals).Lines[0].Name);
        Assert.Equal(200, Assert.Single(read.Days[1].Sports).Calories);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/sport-templates",
            templateRequest with { DistanceKm = 100000000 })).StatusCode);
    }

    [Fact]
    public async Task Two_weeks_multiple_events_portions_snapshots_and_restart()
    {
        var user = Guid.NewGuid();
        Guid weekId;
        Guid mealId;
        await using (var factory = new LifeOSApiFactory(postgres.ConnectionString))
        {
            using var client = factory.CreateClient().AsUser(user);
            var food = await Food(client);
            var week = await Post<ManualWeekDto>(client, "/api/manual-planner/weeks", new ManualWeekRequest(new(2026, 10, 5)));
            weekId = week.Id;
            var other = await Post<ManualWeekDto>(client, "/api/manual-planner/weeks", new ManualWeekRequest(new(2026, 10, 12)));
            var day = week.Days[0];
            var meal = await Post<ManualMealDto>(client, "/api/manual-planner/meals",
                new ManualMealRequest(day.Id, 990, 1005, 1, 2, Lines: [new(food.Id, 100, "g")]));
            mealId = meal.Id;
            Assert.Equal(100, meal.Lines[0].PersonalQuantity);
            Assert.Equal(200, meal.Lines[0].PreparationQuantity);
            Assert.Equal(150d, meal.Nutrition.Calories);
            Assert.True(meal.Nutrition.IsComplete);
            var template = await Post<System.Text.Json.JsonElement>(client, "/api/sport-templates",
                new SportTemplatesEndpoints.SportTemplateRequest("Vélo 10 km", "bike", 30, 10, "moderate", 200));
            var templateId = template.GetProperty("id").GetGuid();
            foreach (var start in new[] { 420, 1020, 1320 })
                await Post<ManualSportDto>(client, "/api/manual-planner/sports",
                    new ManualSportRequest(day.Id, start, start + 30, templateId, "Vélo", "bike", "moderate", 30, 10, 200));
            (await client.PutAsJsonAsync($"/api/food-items/{food.Id}", new
            {
                name = "Riz modifié", referenceUnit = "100g",
                nutrition = new { caloriesPerUnit = 999, proteinsPerUnit = 0, carbsPerUnit = 0, fatsPerUnit = 0 }
            })).EnsureSuccessStatusCode();
            (await client.DeleteAsync($"/api/food-items/{food.Id}")).EnsureSuccessStatusCode();
            (await client.DeleteAsync($"/api/sport-templates/{templateId}")).EnsureSuccessStatusCode();
            var updatedResponse = await client.PutAsJsonAsync($"/api/manual-planner/meals/{meal.Id}",
                new ManualMealRequest(day.Id, 1000, 1015, 2, 0));
            updatedResponse.EnsureSuccessStatusCode();
            var updated = (await updatedResponse.Content.ReadFromJsonAsync<ManualMealDto>())!;
            Assert.Equal(300d, updated.Nutrition.Calories);
            Assert.Equal(200, updated.Lines[0].PreparationQuantity);
            var unchanged = (await client.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{other.Id}"))!;
            Assert.All(unchanged.Days, d => { Assert.Empty(d.Meals); Assert.Empty(d.Sports); });
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/weeks/{week.Id}/balanced-plan/compute", null)).StatusCode);
        }
        await using var restarted = new LifeOSApiFactory(postgres.ConnectionString);
        using var reader = restarted.CreateClient().AsUser(user);
        var persisted = (await reader.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{weekId}"))!;
        Assert.Equal(3, persisted.Days[0].Sports.Count);
        var savedMeal = Assert.Single(persisted.Days[0].Meals);
        Assert.Equal(mealId, savedMeal.Id);
        Assert.Equal(300d, savedMeal.Nutrition.Calories);
        Assert.Equal("Riz", savedMeal.Lines[0].Name);
        Assert.Equal(1000, savedMeal.StartMinute);
    }

    [Fact]
    public async Task Recipe_reference_portions_and_direct_banana_are_independent()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Food(client);
        var article = await Post<System.Text.Json.JsonElement>(client, "/api/articles",
            new { name = "Riz acheté", description = "", unit = "kilogram" });
        var articleId = article.GetProperty("id").GetGuid();
        (await client.PutAsJsonAsync($"/api/food-items/{food.Id}/article", new { articleId })).EnsureSuccessStatusCode();
        var recipe = await Post<RecipeDto>(client, "/api/recipes", new { name = "Petit déjeuner", servings = 2, durationMinutes = 10 });
        (await client.PostAsJsonAsync($"/api/recipes/{recipe.Id}/ingredients",
            new { foodItemId = articleId, quantity = 200, unit = "g" })).EnsureSuccessStatusCode();
        var week = await Post<ManualWeekDto>(client, "/api/manual-planner/weeks", new ManualWeekRequest(new(2026, 10, 5)));
        var meal = await Post<ManualMealDto>(client, "/api/manual-planner/meals", new ManualMealRequest(week.Days[0].Id, 480, 510, 1, 2, recipe.Id));
        Assert.Equal(150d, meal.Nutrition.Calories);
        Assert.Equal(200, meal.Lines[0].PreparationQuantity);
        var banana = await Post<FoodItemDto>(client, "/api/food-items", new
        {
            name = "Banane", referenceUnit = "piece",
            nutrition = new { caloriesPerUnit = 90, proteinsPerUnit = 1, carbsPerUnit = 20, fatsPerUnit = 0 }
        });
        await Post<ManualMealDto>(client, "/api/manual-planner/meals",
            new ManualMealRequest(week.Days[0].Id, 990, 1000, 1, 0, Lines: [new(banana.Id, 1, "piece")]));
        (await client.DeleteAsync($"/api/recipes/{recipe.Id}")).EnsureSuccessStatusCode();
        var read = (await client.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{week.Id}"))!;
        Assert.Equal(240d, read.Days[0].Nutrition.Calories);
        Assert.Equal("Petit déjeuner", read.Days[0].Meals.Single(m => m.Id == meal.Id).Name);
    }

    [Fact]
    public async Task Isolation_validation_missing_nutrition_move_and_delete()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var a = factory.CreateClient().AsUser(Guid.NewGuid());
        using var b = factory.CreateClient().AsUser(Guid.NewGuid());
        var food = await Food(a);
        var week = await Post<ManualWeekDto>(a, "/api/manual-planner/weeks", new ManualWeekRequest(new(2026, 10, 5)));
        var meal = await Post<ManualMealDto>(a, "/api/manual-planner/meals",
            new ManualMealRequest(week.Days[0].Id, 300, 320, 1, 0, Lines: [new(food.Id, 1, "piece")]));
        Assert.False(meal.Nutrition.IsComplete);
        Assert.Null(meal.Nutrition.Calories);
        Assert.NotEmpty(meal.Nutrition.Warnings);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/manual-planner/weeks/{week.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/manual-planner/meals/{meal.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/manual-planner/meals/{meal.Id}",
            new ManualMealRequest(week.Days[1].Id, 100, 110, 1, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync($"/api/manual-planner/meals/{meal.Id}",
            new ManualMealRequest(week.Days[1].Id, 200, 100, 1, 0))).StatusCode);
        (await a.PutAsJsonAsync($"/api/manual-planner/meals/{meal.Id}",
            new ManualMealRequest(week.Days[1].Id, 1320, 1340, 1, 0))).EnsureSuccessStatusCode();
        var read = (await a.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{week.Id}"))!;
        Assert.Empty(read.Days[0].Meals);
        Assert.Single(read.Days[1].Meals);
        (await a.DeleteAsync($"/api/manual-planner/meals/{meal.Id}")).EnsureSuccessStatusCode();
        read = (await a.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{week.Id}"))!;
        Assert.All(read.Days, d => Assert.Empty(d.Meals));
    }
}
