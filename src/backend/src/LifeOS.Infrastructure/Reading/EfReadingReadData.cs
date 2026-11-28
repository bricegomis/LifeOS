using LifeOS.Application.Reading.Contracts;
using LifeOS.Domain.Reading;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Reading;

internal sealed class EfReadingReadData(LifeOSDbContext db) : IReadingReadData
{
    public async Task<IReadOnlyList<Book>> BooksAsync(Guid householdId, CancellationToken ct) =>
        await db.Books.AsNoTracking().Where(b => b.HouseholdId == householdId).ToListAsync(ct);
}
