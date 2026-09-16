using System.Net;
using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Api.Dtos;
using LifeOS.Application.Households;
using LifeOS.Application.Planning;
using LifeOS.Application.Stores;
using LifeOS.Application.WeekContexts;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class BackendTaskIsolationTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Stores_support_household_scoped_crud()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var clientA = factory.CreateClient().AsUser(userA);
        using var clientB = factory.CreateClient().AsUser(userB);

        var createResponse = await clientA.PostAsJsonAsync(
            "/api/stores",
            new StoreRequest("Marché A", "1 rue A", IsOrganic: true, IsLocal: true));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<StoreDto>();
        Assert.NotNull(created);

        var updateResponse = await clientA.PutAsJsonAsync(
            $"/api/stores/{created.Id}",
            new StoreRequest("Marché A+", "2 rue A", IsOrganic: false, IsLocal: true));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<StoreDto>();
        Assert.NotNull(updated);
        Assert.Equal("Marché A+", updated.Name);
        Assert.Equal("2 rue A", updated.Address);
        Assert.False(updated.IsOrganic);

        var getForB = await clientB.GetAsync($"/api/stores/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getForB.StatusCode);

        var updateForB = await clientB.PutAsJsonAsync(
            $"/api/stores/{created.Id}",
            new StoreRequest("Intrusion", "N/A", IsOrganic: false, IsLocal: false));
        Assert.Equal(HttpStatusCode.NotFound, updateForB.StatusCode);

        var deleteForB = await clientB.DeleteAsync($"/api/stores/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteForB.StatusCode);

        var deleteForA = await clientA.DeleteAsync($"/api/stores/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteForA.StatusCode);
    }

    [Fact]
    public async Task Household_members_endpoint_returns_current_household_members_and_profiles()
    {
        var userId = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(userId);

        var response = await client.GetAsync("/api/households/members");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var household = await response.Content.ReadFromJsonAsync<HouseholdMembersResponse>();
        Assert.NotNull(household);
        Assert.Single(household.Members);
        Assert.Single(household.MemberProfiles);
        Assert.Equal(userId, household.Members[0].SupabaseUserId);
        Assert.Equal("owner", household.Members[0].Role);
        Assert.Equal("Moi", household.MemberProfiles[0].DisplayName);
    }

    [Fact]
    public async Task Planning_rules_frequency_rules_and_week_context_persist_by_household()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        Guid createdPlanningRuleId;
        Guid createdFrequencyRuleId;

        await using (var firstRun = new LifeOSApiFactory(postgres.ConnectionString))
        {
            using var clientA = firstRun.CreateClient().AsUser(userA);
            using var clientB = firstRun.CreateClient().AsUser(userB);

            var planningResponse = await clientA.PostAsJsonAsync(
                "/api/planning-rules",
                new PlanningRuleRequest(
                    "monday",
                    "lunch",
                    new PlanningRuleTargetDto("dish", null, null, "leftovers")));
            Assert.Equal(HttpStatusCode.Created, planningResponse.StatusCode);

            var planningRule = await planningResponse.Content.ReadFromJsonAsync<PlanningRuleDto>();
            Assert.NotNull(planningRule);
            createdPlanningRuleId = planningRule.Id;

            var frequencyResponse = await clientA.PostAsJsonAsync(
                "/api/frequency-rules",
                new FrequencyRuleRequest(
                    new FrequencyRuleTargetDto("category", null, null, "batch-cooking", "Batch cooking"),
                    TargetCountPerWeek: 2));
            Assert.Equal(HttpStatusCode.Created, frequencyResponse.StatusCode);

            var frequencyRule = await frequencyResponse.Content.ReadFromJsonAsync<FrequencyRuleDto>();
            Assert.NotNull(frequencyRule);
            createdFrequencyRuleId = frequencyRule.Id;

            var weekContext = new WeekContextDto(
                new AlternatingWeekConfigDto("2026-09-14", "kids"),
                [new WeekModeOverrideDto("2026-09-21", "solo")],
                new Dictionary<string, DayContextDto>
                {
                    ["monday"] = new("office", BikeCommute: true),
                    ["tuesday"] = new("home", BikeCommute: false),
                    ["wednesday"] = new("home", BikeCommute: false),
                    ["thursday"] = new("office", BikeCommute: true),
                    ["friday"] = new("off", BikeCommute: false),
                    ["saturday"] = new("home", BikeCommute: false),
                    ["sunday"] = new("home", BikeCommute: false),
                });

            var saveContextResponse = await clientA.PutAsJsonAsync("/api/week-context", weekContext);
            Assert.Equal(HttpStatusCode.OK, saveContextResponse.StatusCode);

            var planningRulesForB = await clientB.GetFromJsonAsync<List<PlanningRuleDto>>("/api/planning-rules");
            Assert.DoesNotContain(planningRulesForB!, rule => rule.Id == createdPlanningRuleId);

            var frequencyRulesForB = await clientB.GetFromJsonAsync<List<FrequencyRuleDto>>("/api/frequency-rules");
            Assert.DoesNotContain(frequencyRulesForB!, rule => rule.Id == createdFrequencyRuleId);
        }

        await using var secondRun = new LifeOSApiFactory(postgres.ConnectionString);
        using var reconnectedClient = secondRun.CreateClient().AsUser(userA);

        var planningRules = await reconnectedClient.GetFromJsonAsync<List<PlanningRuleDto>>("/api/planning-rules");
        Assert.Contains(planningRules!, rule => rule.Id == createdPlanningRuleId && rule.Target.DishId == "leftovers");

        var frequencyRules = await reconnectedClient.GetFromJsonAsync<List<FrequencyRuleDto>>("/api/frequency-rules");
        Assert.Contains(frequencyRules!, rule => rule.Id == createdFrequencyRuleId && rule.TargetCountPerWeek == 2);

        var persistedContext = await reconnectedClient.GetFromJsonAsync<WeekContextDto>("/api/week-context");
        Assert.NotNull(persistedContext);
        Assert.Equal("kids", persistedContext.AlternatingWeekConfig.ReferenceWeekMode);
        Assert.True(persistedContext.Days["monday"].BikeCommute);
        Assert.Equal("office", persistedContext.Days["monday"].WorkLocation);
    }

    [Fact]
    public async Task Day_plans_planned_meals_and_parts_are_household_scoped()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var clientA = factory.CreateClient().AsUser(userA);
        using var clientB = factory.CreateClient().AsUser(userB);

        var recipeAResponse = await clientA.PostAsJsonAsync("/api/recipes", new CreateRecipeRequest("A recipe", 2, 20));
        Assert.Equal(HttpStatusCode.Created, recipeAResponse.StatusCode);
        var recipeA = await recipeAResponse.Content.ReadFromJsonAsync<RecipeDto>();
        Assert.NotNull(recipeA);

        var recipeBResponse = await clientB.PostAsJsonAsync("/api/recipes", new CreateRecipeRequest("B recipe", 2, 20));
        Assert.Equal(HttpStatusCode.Created, recipeBResponse.StatusCode);
        var recipeB = await recipeBResponse.Content.ReadFromJsonAsync<RecipeDto>();
        Assert.NotNull(recipeB);

        var weekResponse = await clientA.PostAsJsonAsync("/api/weeks", new CreateWeekRequest(new DateOnly(2026, 9, 28)));
        Assert.Equal(HttpStatusCode.Created, weekResponse.StatusCode);
        var week = await weekResponse.Content.ReadFromJsonAsync<WeekDto>();
        Assert.NotNull(week);

        var dayPlan = week.DayPlans[0];

        var crossHouseholdRecipeResponse = await clientA.PostAsJsonAsync(
            $"/api/planned-meals/{dayPlan.Id}/meals",
            new CreatePlannedMealRequest("lunch", RecipeId: recipeB.Id));
        Assert.Equal(HttpStatusCode.NotFound, crossHouseholdRecipeResponse.StatusCode);

        var mealResponse = await clientA.PostAsJsonAsync(
            $"/api/planned-meals/{dayPlan.Id}/meals",
            new CreatePlannedMealRequest("lunch", RecipeId: recipeA.Id));
        Assert.Equal(HttpStatusCode.Created, mealResponse.StatusCode);

        var plannedMeal = await mealResponse.Content.ReadFromJsonAsync<PlannedMealDto>();
        Assert.NotNull(plannedMeal);

        var householdMembers = await clientA.GetFromJsonAsync<HouseholdMembersResponse>("/api/households/members");
        Assert.NotNull(householdMembers);
        var memberProfileId = householdMembers.MemberProfiles[0].Id;

        var partResponse = await clientA.PostAsJsonAsync(
            $"/api/planned-meals/{plannedMeal.Id}/parts",
            new AddPlannedMealPartRequest(memberProfileId, 1.25m));
        Assert.Equal(HttpStatusCode.OK, partResponse.StatusCode);

        var mealWithPart = await partResponse.Content.ReadFromJsonAsync<PlannedMealDto>();
        Assert.NotNull(mealWithPart);
        Assert.Contains(mealWithPart.Parts, part => part.MemberProfileId == memberProfileId && part.PortionMultiplier == 1.25m);

        var dayPlanForB = await clientB.GetAsync($"/api/day-plans/{dayPlan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, dayPlanForB.StatusCode);

        var plannedMealForB = await clientB.GetAsync($"/api/planned-meals/{plannedMeal.Id}");
        Assert.Equal(HttpStatusCode.NotFound, plannedMealForB.StatusCode);

        var partForB = await clientB.PostAsJsonAsync(
            $"/api/planned-meals/{plannedMeal.Id}/parts",
            new AddPlannedMealPartRequest(memberProfileId, 1m));
        Assert.Equal(HttpStatusCode.NotFound, partForB.StatusCode);
    }

    private sealed record HouseholdMembersResponse(
        Guid Id,
        List<HouseholdMemberDto> Members,
        List<MemberProfileDto> MemberProfiles);
}
