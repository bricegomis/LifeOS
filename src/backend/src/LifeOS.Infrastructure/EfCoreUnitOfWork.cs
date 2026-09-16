using LifeOS.Application.Common.Interfaces;
using LifeOS.Infrastructure.Persistence;

namespace LifeOS.Infrastructure;

/// <summary>
/// EF Core implementation of the unit of work pattern.
/// Coordinates transaction management across repositories.
/// </summary>
public sealed class EfCoreUnitOfWork : IUnitOfWork
{
    private readonly LifeOSDbContext _context;

    public EfCoreUnitOfWork(LifeOSDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
