using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Households;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Households;

/// <summary>
/// EF Core / PostgreSQL implementation of <see cref="IHouseholdRepository"/>.
/// </summary>
public sealed class EfHouseholdRepository(LifeOSDbContext dbContext) : IHouseholdRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<Household?> FindBySupabaseUserIdAsync(Guid supabaseUserId, CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.SupabaseUserId == supabaseUserId, cancellationToken);

        if (member is null)
        {
            return null;
        }

        return await _dbContext.Households
            .AsNoTracking()
            .Include(household => household.Members)
            .Include(household => household.MemberProfiles)
            .FirstOrDefaultAsync(household => household.Id == member.HouseholdId, cancellationToken);
    }

    public async Task AddAsync(Household household, CancellationToken cancellationToken = default)
    {
        _dbContext.Households.Add(household);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.Entry(household).State = EntityState.Detached;
            throw new DuplicateHouseholdMemberException();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }
}
