using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LifeOS.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations</c>. Never used at runtime: the real
/// connection string is resolved via <see cref="PostgresConnectionStringResolver"/> from
/// configuration / environment variables (ADR 0001), never committed here. A local default is
/// used only to generate migrations against a developer's own PostgreSQL instance.
/// </summary>
public sealed class LifeOSDbContextFactory : IDesignTimeDbContextFactory<LifeOSDbContext>
{
    public LifeOSDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=lifeos;Username=lifeos;Password=lifeos";

        var optionsBuilder = new DbContextOptionsBuilder<LifeOSDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LifeOSDbContext(optionsBuilder.Options);
    }
}
