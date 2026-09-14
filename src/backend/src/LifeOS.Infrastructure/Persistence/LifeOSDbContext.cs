using LifeOS.Domain.Articles;
using LifeOS.Domain.Households;
using LifeOS.Domain.Stores;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Persistence;

/// <summary>
/// EF Core / Npgsql database context backing the PostgreSQL business database (ADR 0001).
/// Only the domains migrated so far (households, stores, articles) are mapped here; other
/// bounded contexts remain in-memory until they are ported in later technical milestones.
/// </summary>
public sealed class LifeOSDbContext(DbContextOptions<LifeOSDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<GroceryItem> GroceryItems => Set<GroceryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeOSDbContext).Assembly);
    }
}
