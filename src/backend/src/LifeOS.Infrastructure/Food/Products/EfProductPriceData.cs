using LifeOS.Application.Food.Products;
using LifeOS.Domain.Food.Products;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Food.Products;

internal sealed class EfProductPriceData(LifeOSDbContext db) : IProductPriceData
{
    public Task<Product?> FindProductAsync(Guid householdId, Guid productId, CancellationToken ct) =>
        db.Products.Include(p => p.Prices).SingleOrDefaultAsync(p => p.Id == productId && p.HouseholdId == householdId, ct);

    public Task<bool> StoreBelongsToHouseholdAsync(Guid householdId, Guid storeId, CancellationToken ct) =>
        db.Stores.AnyAsync(s => s.Id == storeId && s.HouseholdId == householdId, ct);

    // Added through the DbSet so EF inserts the row instead of guessing its state from the key.
    public void AddPrice(ProductPrice price) => db.ProductPrices.Add(price);

    public void RemovePrice(ProductPrice price) => db.ProductPrices.Remove(price);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
