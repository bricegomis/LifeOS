using System.Net.Http.Json;
using LifeOS.Api.Contracts;
using LifeOS.Application.FoodItems;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LifeOS.Api.IntegrationTests;

public sealed class UnifiedProductsMigrationTests
{
    private static PostgreSqlContainer Database() => new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("products_upgrade").WithUsername("products_upgrade").WithPassword("products_upgrade").Build();

    [Fact]
    public async Task Linked_unlinked_non_food_prices_references_off_corrections_and_snapshots_survive_upgrade_and_restart()
    {
        await using var container = Database();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<LifeOSDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        var household = Guid.NewGuid();
        var user = Guid.NewGuid();
        var foreignHousehold = Guid.NewGuid();
        var article = Guid.NewGuid();
        var food = Guid.NewGuid();
        var unlinkedFood = Guid.NewGuid();
        var unlinkedArticle = Guid.NewGuid();
        var soap = Guid.NewGuid();
        var correction = Guid.NewGuid();
        var store = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var recipe = Guid.NewGuid();
        var ingredient = Guid.NewGuid();
        var stockId = Guid.NewGuid();
        var shoppingId = Guid.NewGuid();
        var week = Guid.NewGuid();
        var day = Guid.NewGuid();
        var meal = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var payload = """{"product_name":"Riz"}""";
        await using (var db = new LifeOSDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261007065024_PlannerQuantityPrecision");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO households ("Id","Name","CreatedAt","UpdatedAt")
                  VALUES ({household},'Foyer',now(),now()), ({foreignHousehold},'Autre foyer',now(),now());
                INSERT INTO household_members ("Id","HouseholdId","SupabaseUserId","Role","CreatedAt")
                  VALUES ({Guid.NewGuid()},{household},{user},'Owner',now());
                INSERT INTO stores ("Id","HouseholdId","Name","Address","CreatedAt","UpdatedAt","IsOrganic","IsLocal")
                  VALUES ({store},{household},'Marché','',now(),now(),false,false);
                INSERT INTO articles ("Id","HouseholdId","Name","Description","Unit","CreatedAt","UpdatedAt")
                  VALUES ({article},{household},'Ancien nom achat','Description achat','Kilogram',{timestamp},{timestamp}),
                         ({unlinkedArticle},{household},'Même nom','Non associé','Liter',{timestamp},{timestamp}),
                         ({soap},{household},'Savon','Non alimentaire','Unit',{timestamp},{timestamp});
                INSERT INTO food_items (id,household_id,name,reference_unit,source,off_barcode,off_payload,
                                        created_at,updated_at,calories_per_unit,proteins_per_unit,carbs_per_unit,fats_per_unit,"ArticleId","IsArchived")
                  VALUES ({food},{household},'Riz nutrition','100g','OpenFoodFacts','123456',
                          {payload}::jsonb,{timestamp},{timestamp},150,3,30,1,{article},true),
                         ({unlinkedFood},{household},'Même nom','100ml','Manual',NULL,NULL,{timestamp},{timestamp},70,2,10,1,NULL,false),
                         ({Guid.NewGuid()},{foreignHousehold},'Même nom','piece','Manual',NULL,NULL,{timestamp},{timestamp},NULL,NULL,NULL,NULL,NULL,false);
                INSERT INTO food_items (id,household_id,name,reference_unit,source,is_correction_of,created_at,updated_at,calories_per_unit)
                  VALUES ({correction},{household},'Riz corrigé','100g','Manual',{food},{timestamp},{timestamp},140);
                INSERT INTO article_price_entries ("Id","ArticleId","StoreId","Price","ObservedAt","CreatedAt")
                  VALUES ({priceId},{article},{store},2.50,{timestamp},{timestamp});
                INSERT INTO recipes ("Id","HouseholdId","Name","Servings","DurationMinutes","Tags","CreatedAt","UpdatedAt")
                  VALUES ({recipe},{household},'Recette historique',3,10,'',{timestamp},{timestamp});
                INSERT INTO recipe_ingredients ("Id","RecipeId","FoodItemId","Quantity","Unit")
                  VALUES ({ingredient},{recipe},{article},123.456789,'g');
                INSERT INTO weeks ("Id","HouseholdId","StartsOn","Status","WeekMode","CreatedAt","UpdatedAt","IsManual")
                  VALUES ({week},{household},'2026-10-05','draft','Kids',{timestamp},{timestamp},true);
                INSERT INTO day_plans ("Id","WeekId","Date","WorkContext","BikeCommute")
                  VALUES ({day},{week},'2026-10-05','home',false);
                INSERT INTO planned_meals ("Id","DayPlanId","MealType","Status","CreatedAt","UpdatedAt","ContentName","PersonalPortion","ChildrenCount","StartMinute","EndMinute")
                  VALUES ({meal},{day},'lunch','planned',{timestamp},{timestamp},'Nom figé',1.5,2,720,750);
                INSERT INTO meal_food_lines ("Id","PlannedMealId","FoodItemId","Name","Quantity","Unit","ReferenceUnit","Calories","Protein","Carbs","Fat")
                  VALUES ({Guid.NewGuid()},{meal},{food},'Ancien nom figé',41.152263000123,'g','100g',111,2,20,1);
                INSERT INTO stock_items ("Id","HouseholdId","GroceryItemId","Quantity","Unit","CreatedAt","UpdatedAt")
                  VALUES ({stockId},{household},{article},2.25,'kg',{timestamp},{timestamp});
                INSERT INTO shopping_list_items ("Id","HouseholdId","WeekId","GroceryItemId","QuantityNeeded","QuantityFromStock","Checked","CreatedAt","UpdatedAt")
                  VALUES ({shoppingId},{household},{week},{article},3.25,1.25,true,{timestamp},{timestamp});
                """);
            await db.GetService<IMigrator>().MigrateAsync("20261009023138_UnifiedProducts");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO article_price_entries ("Id","ArticleId","StoreId","Price","ObservedAt","CreatedAt")
                VALUES ({Guid.NewGuid()},{unlinkedFood},{store},7,{timestamp},{timestamp});
                """);
            await db.Database.MigrateAsync();
        }
        await using (var db = new LifeOSDbContext(options))
        {
            Assert.Equal(4, await db.FoodItems.CountAsync());
            Assert.Equal(6, await db.Products.CountAsync());
            var product = await db.Products.SingleAsync(p => p.Id == food);
            var aliment = await db.FoodItems.SingleAsync(p => p.Id == food);
            Assert.True(product.IsArchived);
            Assert.Equal("Riz nutrition", product.Name);
            Assert.Equal("Ancien nom achat", product.LegacyPurchaseName);
            Assert.Equal("Description achat", product.Description);
            Assert.Equal("100g", aliment.ReferenceUnit);
            Assert.Equal(150, aliment.Nutrition!.CaloriesPerUnit);
            Assert.Equal("123456", aliment.OffBarcode);
            Assert.Contains("Riz", aliment.OffPayload!);
            Assert.Equal(timestamp, product.CreatedAt);
            var price = Assert.Single(product.PriceHistory);
            Assert.Equal(priceId, price.Id);
            Assert.Equal(store, price.StoreId);
            Assert.Equal(2.50m, price.Price);
            Assert.Equal(timestamp, price.ObservedAt);
            Assert.Empty(await db.Recipes.ToListAsync());
            Assert.Empty(await db.RecipeIngredients.ToListAsync());
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM planned_meals").SingleAsync());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM stock_items").SingleAsync());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM shopping_list_items").SingleAsync());
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'meal_food_lines'").SingleAsync());
            Assert.Equal(food, (await db.FoodItems.SingleAsync(p => p.Id == correction)).IsCorrectionOf);
            Assert.Null((await db.Products.SingleAsync(p => p.Id == soap)).FoodItemId);
            Assert.False(await db.FoodItems.AnyAsync(p => p.Id == soap));
            Assert.Null((await db.Products.SingleAsync(p => p.Id == unlinkedArticle)).FoodItemId);
            Assert.Equal(unlinkedFood, (await db.Products.SingleAsync(p => p.Id == unlinkedFood)).FoodItemId);
            Assert.NotEqual(unlinkedFood, unlinkedArticle);
            Assert.Single(await db.FoodItems.Where(p => p.HouseholdId == household && p.Name == "Même nom").ToListAsync());
            Assert.Equal(food, await db.ResolveProductIdAsync(household, article, default));
            Assert.Equal(article, await db.ResolveProductIdAsync(foreignHousehold, article, default));
            Assert.Equal(3, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM legacy_articles").SingleAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            var unknown = await db.Products.SingleAsync(p => p.Id == unlinkedFood);
            Assert.False(unknown.PurchaseUnitConfirmed);
            Assert.Equal("unknown", Assert.Single(unknown.PriceHistory).Unit);
            Assert.Empty(LifeOS.Domain.Products.ComparablePrices.Minimums([unknown]));
            Assert.Throws<ArgumentException>(() => unknown.ObservePrice(store, 8, timestamp));
            unknown.Update(unknown.Name, unknown.Description, "bag", null, null, 400m, "g", true, false);
            unknown.ObservePrice(store, 8, timestamp.AddDays(1));
            Assert.Equal("unknown", unknown.PriceHistory[0].Unit);
            Assert.Equal(20m, Assert.Single(LifeOS.Domain.Products.ComparablePrices.Minimums([unknown])).Price);
            await db.SaveChangesAsync();
            db.Database.ExecuteSqlRaw("UPDATE products SET \"IsArchived\" = false WHERE id = {0}", food);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ambiguous_collision_or_cross_household_link_aborts_atomically(bool collision)
    {
        await using var container = Database();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<LifeOSDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new LifeOSDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261007065024_PlannerQuantityPrecision");
        var household = Guid.NewGuid();
        var other = Guid.NewGuid();
        var article = Guid.NewGuid();
        var food = collision ? article : Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO households ("Id","Name","CreatedAt","UpdatedAt")
              VALUES ({household},'Foyer',now(),now()),({other},'Autre',now(),now());
            INSERT INTO articles ("Id","HouseholdId","Name","Description","Unit","CreatedAt","UpdatedAt")
              VALUES ({article},{household},'Nom identique','','Unit',now(),now());
            INSERT INTO food_items (id,household_id,name,reference_unit,source,created_at,updated_at,"ArticleId")
              VALUES ({food},{(collision ? household : other)},'Nom identique','piece','Manual',now(),now(),{(collision ? (Guid?)null : article)});
            """);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
        Assert.Contains("Resolution explicite", exception.MessageText);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM articles").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM food_items").SingleAsync());
        Assert.DoesNotContain(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("UnifiedProducts"));
    }

    [Fact]
    public async Task Cross_household_food_usage_aborts_the_new_migration_without_partial_catalogue()
    {
        await using var container = Database();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<LifeOSDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new LifeOSDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261009023138_UnifiedProducts");
        var household = Guid.NewGuid(); var other = Guid.NewGuid(); var product = Guid.NewGuid(); var recipe = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO households ("Id","Name","CreatedAt","UpdatedAt") VALUES
              ({household},'Foyer',now(),now()),({other},'Autre',now(),now());
            INSERT INTO products (id,household_id,name,reference_unit,source,"Description","Unit",created_at,updated_at)
              VALUES ({product},{household},'Aliment','100g','Manual','','Unit',now(),now());
            INSERT INTO recipes ("Id","HouseholdId","Name","Servings","DurationMinutes","Tags","CreatedAt","UpdatedAt")
              VALUES ({recipe},{other},'Recette',1,10,'',now(),now());
            INSERT INTO recipe_ingredients ("Id","RecipeId","FoodItemId","Quantity","Unit")
              VALUES ({Guid.NewGuid()},{recipe},{product},1,'g');
            """);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
        Assert.Contains("FoodsAndReceipts", exception.MessageText);
        Assert.DoesNotContain(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("FoodsAndReceipts"));
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM products").SingleAsync());
        Assert.Null(await db.Database.SqlQueryRaw<string?>("SELECT to_regclass('foods')::text AS \"Value\"").SingleAsync());
        Assert.Null(await db.Database.SqlQueryRaw<string?>("SELECT to_regclass('receipts')::text AS \"Value\"").SingleAsync());
    }
}
