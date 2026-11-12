using LifeOS.Application.Food.Receipts;
using LifeOS.Domain.Food.Receipts;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Food.Receipts;

internal sealed class EfReceiptData(LifeOSDbContext db) : IReceiptData
{
    public Task<Receipt?> FindAsync(Guid householdId, Guid receiptId, CancellationToken ct) =>
        db.Receipts.SingleOrDefaultAsync(r => r.Id == receiptId && r.HouseholdId == householdId, ct);

    public Task<bool> StoreBelongsToHouseholdAsync(Guid householdId, Guid storeId, CancellationToken ct) =>
        db.Stores.AnyAsync(s => s.Id == storeId && s.HouseholdId == householdId, ct);

    public Task<bool> PhotoExistsAsync(Guid householdId, string sha256, CancellationToken ct) =>
        db.Receipts.AnyAsync(r => r.HouseholdId == householdId && r.DocumentHash == sha256, ct);

    public void Add(Receipt receipt) => db.Receipts.Add(receipt);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
