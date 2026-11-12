using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Households;

public sealed class EfUserAccountRepository(LifeOSDbContext dbContext) : IUserAccountRepository
{
    public Task<UserAccount?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken = default) =>
        dbContext.UserAccounts.FirstOrDefaultAsync(user => user.GoogleSubject == googleSubject, cancellationToken);

    public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.UserAccounts.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task SaveAsync(UserAccount account, bool isNew, CancellationToken cancellationToken = default)
    {
        if (isNew) dbContext.UserAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
