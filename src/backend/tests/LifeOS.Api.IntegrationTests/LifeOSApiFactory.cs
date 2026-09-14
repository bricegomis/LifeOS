using LifeOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Boots the real API (with its EF Core migrations applied on startup, see <c>Program.cs</c>)
/// against a given PostgreSQL connection string, so integration tests exercise the actual
/// persistence path end-to-end (ADR 0001) rather than a fake in-memory substitute.
/// Authentication is swapped for <see cref="Authentication.TestAuthHandler"/> via the "Testing"
/// ASP.NET Core environment.
/// </summary>
public sealed class LifeOSApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["Supabase:Url"] = "https://test.supabase.co",
            });
        });
    }

    /// <summary>
    /// Creates a scoped <see cref="LifeOSDbContext"/> for test setup/assertions that bypass the
    /// HTTP API (e.g. seeding another household's data to prove isolation).
    /// </summary>
    public LifeOSDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<LifeOSDbContext>();
    }
}
