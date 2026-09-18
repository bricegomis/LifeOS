using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Locks the schema-level integrity constraints added on <c>food_items</c> (migration
/// <c>StabilizeFoodItemForeignKeys</c>): a real foreign key to <c>households</c> (so a food item
/// can never outlive its household) and a self-referencing foreign key on
/// <c>is_correction_of</c> (so a correction can never point at a non-existent food item).
/// Exercised directly against <see cref="LifeOSDbContext"/> rather than the HTTP API, since these
/// are database-level invariants rather than application-layer behavior.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FoodItemSchemaConstraintsTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private LifeOSApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new LifeOSApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Deleting_a_household_cascades_to_its_food_items()
    {
        await using var dbContext = _factory.CreateDbContext();

        var household = Household.CreateForOwner(Guid.NewGuid(), "Foyer test");
        dbContext.Households.Add(household);

        var foodItem = FoodItem.CreateManual(household.Id, "Banane", "piece");
        dbContext.FoodItems.Add(foodItem);

        await dbContext.SaveChangesAsync();

        dbContext.Households.Remove(household);
        await dbContext.SaveChangesAsync();

        var stillExists = await dbContext.FoodItems.AnyAsync(f => f.Id == foodItem.Id);
        Assert.False(stillExists, "The food item must be cascade-deleted with its household.");
    }

    [Fact]
    public async Task Inserting_a_food_item_for_an_unknown_household_is_rejected()
    {
        await using var dbContext = _factory.CreateDbContext();

        var foodItem = FoodItem.CreateManual(Guid.NewGuid(), "Orange", "piece");
        dbContext.FoodItems.Add(foodItem);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_the_original_food_item_nulls_out_the_correction_reference()
    {
        await using var dbContext = _factory.CreateDbContext();

        var household = Household.CreateForOwner(Guid.NewGuid(), "Foyer test");
        dbContext.Households.Add(household);

        var original = FoodItem.CreateManual(household.Id, "Oeuf", "piece");
        dbContext.FoodItems.Add(original);
        await dbContext.SaveChangesAsync();

        var correction = FoodItem.CreateCorrection(household.Id, "Oeuf (gros)", "piece", null, original.Id);
        dbContext.FoodItems.Add(correction);
        await dbContext.SaveChangesAsync();

        dbContext.FoodItems.Remove(original);
        await dbContext.SaveChangesAsync();

        // Reload from the database (not the change tracker) to observe the DB-applied SET NULL.
        await using var verifyContext = _factory.CreateDbContext();
        var reloadedCorrection = await verifyContext.FoodItems.SingleAsync(f => f.Id == correction.Id);

        Assert.Null(reloadedCorrection.IsCorrectionOf);
    }
}
