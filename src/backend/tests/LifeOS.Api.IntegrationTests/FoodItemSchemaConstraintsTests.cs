using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class FoodItemSchemaConstraintsTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Unknown_households_cross_household_links_and_unknown_classification_codes_are_rejected_by_database()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        await using (var db = factory.CreateDbContext())
        {
            db.FoodItems.Add(FoodItem.CreateManual(Guid.NewGuid(), "Orange", "fruit", "1 pièce"));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = factory.CreateDbContext())
        {
            var ownerA = UserAccount.Create($"a-{Guid.NewGuid():N}", "a@test.lifeos", null, DateTimeOffset.UtcNow);
            var ownerB = UserAccount.Create($"b-{Guid.NewGuid():N}", "b@test.lifeos", null, DateTimeOffset.UtcNow);
            db.UserAccounts.AddRange(ownerA, ownerB);
            var a = Household.CreateForOwner(ownerA.Id, "A");
            var b = Household.CreateForOwner(ownerB.Id, "B");
            db.Households.AddRange(a, b);
            var food = FoodItem.CreateManual(a.Id, "Aliment", "other", "");
            db.FoodItems.Add(food);
            await db.SaveChangesAsync();
            db.Products.Add(Product.Create(b.Id, food.Id, "Produit", 1, "kg"));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = factory.CreateDbContext())
        {
            var food = await db.FoodItems.FirstAsync();
            foreach (var sql in new[]
            {
                "UPDATE food.foods SET family = 'soja' WHERE id = {0}",
                "UPDATE food.foods SET roles = ARRAY['main'] WHERE id = {0}",
                "UPDATE food.foods SET conservation = 'fridge' WHERE id = {0}",
                "UPDATE food.foods SET season_months = ARRAY[13] WHERE id = {0}",
            })
                await Assert.ThrowsAnyAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, food.Id));
        }
    }
}
