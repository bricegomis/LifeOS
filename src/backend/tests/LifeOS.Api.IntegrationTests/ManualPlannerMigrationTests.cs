using System.Net.Http.Json;
using LifeOS.Api.Dtos;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace LifeOS.Api.IntegrationTests;

public sealed class ManualPlannerMigrationTests
{
    [Fact]
    public async Task Existing_composed_meals_prices_and_unpositioned_sessions_survive_upgrade()
    {
        await using var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("upgrade_test").WithUsername("upgrade_test").WithPassword("upgrade_test").Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<LifeOSDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        var household = Guid.NewGuid();
        var user = Guid.NewGuid();
        var week = Guid.NewGuid();
        var day = Guid.NewGuid();
        var recipe = Guid.NewGuid();
        var composed = Guid.NewGuid();
        var article = Guid.NewGuid();
        var store = Guid.NewGuid();
        var meal = Guid.NewGuid();
        var session = Guid.NewGuid();
        await using (var db = new LifeOSDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261006143747_SeparateWeekTypes");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO households ("Id","Name","CreatedAt","UpdatedAt") VALUES ({household},'Historique',now(),now());
                INSERT INTO household_members ("Id","HouseholdId","SupabaseUserId","Role","CreatedAt")
                  VALUES ({Guid.NewGuid()},{household},{user},'Owner',now());
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO stores ("Id","HouseholdId","Name","Address","CreatedAt","UpdatedAt","IsOrganic","IsLocal")
                  VALUES ({store},{household},'Magasin','',now(),now(),false,false);
                INSERT INTO articles ("Id","HouseholdId","Name","Description","Unit","CreatedAt","UpdatedAt")
                  VALUES ({article},{household},'Riz','Achat historique','Kilogram',now(),now());
                INSERT INTO article_price_entries ("Id","ArticleId","StoreId","Price","ObservedAt","CreatedAt")
                  VALUES ({Guid.NewGuid()},{article},{store},2.5,now(),now());
                INSERT INTO recipes ("Id","HouseholdId","Name","Servings","DurationMinutes","Tags","CreatedAt","UpdatedAt")
                  VALUES ({recipe},{household},'Riz ancien',2,10,'',now(),now());
                INSERT INTO recipe_ingredients ("Id","RecipeId","FoodItemId","Quantity","Unit")
                  VALUES ({Guid.NewGuid()},{recipe},{article},200,'g');
                INSERT INTO composed_meals ("Id","HouseholdId","Name","CreatedAt","UpdatedAt")
                  VALUES ({composed},{household},'Repas composé ancien',now(),now());
                INSERT INTO composed_meal_parts ("Id","ComposedMealId","RecipeId","QuantityFactor")
                  VALUES ({Guid.NewGuid()},{composed},{recipe},1);
                INSERT INTO weeks ("Id","HouseholdId","StartsOn","Status","WeekMode","CreatedAt","UpdatedAt")
                  VALUES ({week},{household},'2026-10-05','draft','Kids',now(),now());
                INSERT INTO day_plans ("Id","WeekId","Date","WorkContext","BikeCommute")
                  VALUES ({day},{week},'2026-10-05','office',true);
                INSERT INTO planned_meals ("Id","DayPlanId","MealType","Status","ComposedMealId","CreatedAt","UpdatedAt")
                  VALUES ({meal},{day},'lunch','planned',{composed},now(),now());
                INSERT INTO activity_sessions ("Id","DayPlanId","Type","Intensity","DurationMinutes","EstimatedEnergyKcal","CreatedAt","UpdatedAt")
                  VALUES ({session},{day},'bike','moderate',30,200,now(),now());
                """);
        }
        await using var factory = new LifeOSApiFactory(container.GetConnectionString());
        using var client = factory.CreateClient().AsUser(user);
        var upgraded = (await client.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{week}"))!;
        var oldMeal = Assert.Single(Assert.Single(upgraded.Days).Meals);
        Assert.Equal("Repas composé ancien", oldMeal.Name);
        Assert.Null(oldMeal.StartMinute);
        Assert.Null(oldMeal.EndMinute);
        Assert.False(oldMeal.HasSnapshot);
        Assert.False(oldMeal.Nutrition.IsComplete);
        Assert.Equal(100, Assert.Single(oldMeal.Lines).Quantity);
        Assert.Null(Assert.Single(upgraded.Days[0].Sports).StartMinute);
        (await client.PutAsJsonAsync($"/api/manual-planner/meals/{meal}",
            new ManualMealRequest(day, 720, 750, 1, 2))).EnsureSuccessStatusCode();
        var positioned = (await client.GetFromJsonAsync<ManualWeekDto>($"/api/manual-planner/weeks/{week}"))!;
        Assert.True(positioned.Days[0].Meals[0].HasSnapshot);
        Assert.Equal(composed, positioned.Days[0].Meals[0].ComposedMealId);
        Assert.Equal(200, positioned.Days[0].Meals[0].Lines[0].PreparationQuantity);
        await using var verify = new LifeOSDbContext(options);
        Assert.Equal(1, await verify.GroceryItems.CountAsync());
        Assert.Equal(1, await verify.ComposedMealParts.CountAsync());
        var purchase = await verify.GroceryItems.Include(a => a.PriceHistory).SingleAsync();
        Assert.Equal(2.5m, Assert.Single(purchase.PriceHistory).Price);
        Assert.Equal("Kids", (await verify.Weeks.SingleAsync()).WeekMode.ToString());
    }
}
