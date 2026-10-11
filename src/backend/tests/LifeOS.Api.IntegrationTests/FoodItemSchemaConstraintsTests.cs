using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Products;
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
            db.FoodItems.Add(FoodItem.CreateManual(Guid.NewGuid(), "Orange", "piece"));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = factory.CreateDbContext())
        {
            var a = Household.CreateForOwner(Guid.NewGuid(), "A");
            var b = Household.CreateForOwner(Guid.NewGuid(), "B");
            db.Households.AddRange(a, b);
            var food = FoodItem.CreateManual(a.Id, "Aliment", "");
            db.FoodItems.Add(food);
            await db.SaveChangesAsync();
            db.Products.Add(Product.Create(b.Id, food.Id, "Produit", "unit"));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
