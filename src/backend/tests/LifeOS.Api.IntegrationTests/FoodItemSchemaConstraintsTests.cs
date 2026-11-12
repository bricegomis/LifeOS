using LifeOS.Domain.Food.Foods;
using LifeOS.Domain.Food.Products;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class FoodItemSchemaConstraintsTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Unknown_households_and_cross_household_product_links_are_rejected_by_database()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        await using (var db = factory.CreateDbContext())
        {
            db.FoodItems.Add(FoodItem.CreateManual(Guid.NewGuid(), "Orange", "1 pièce"));
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
            var food = FoodItem.CreateManual(a.Id, "Aliment", "");
            db.FoodItems.Add(food);
            await db.SaveChangesAsync();
            db.Products.Add(Product.Create(b.Id, food.Id, "Produit", 1, "kg"));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
