using LifeOS.Infrastructure.Persistence;
using LifeOS.Application.Receipts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Boots the real API (with its EF Core migrations applied on startup, see <c>Program.cs</c>)
/// against a given PostgreSQL connection string, so integration tests exercise the actual
/// persistence path end-to-end (ADR 0001) rather than a fake in-memory substitute.
/// Authentication is swapped for <see cref="Authentication.TestAuthHandler"/> via the "Testing"
/// ASP.NET Core environment.
/// </summary>
public sealed class LifeOSApiFactory(
    string connectionString,
    bool enableOpenApiUi = true,
    string buildId = "local",
    string? receiptStoragePath = null,
    IReceiptOcrService? receiptOcrService = null)
    : WebApplicationFactory<Program>
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
                ["Api:EnableOpenApiUi"] = enableOpenApiUi.ToString(),
                ["LifeOS:BuildId"] = buildId,
                ["Receipts:StoragePath"] = receiptStoragePath ?? Path.Combine(Path.GetTempPath(), $"lifeos-api-tests-{Guid.NewGuid():N}"),
            });
        });

        if (receiptOcrService is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IReceiptOcrService>();
                services.AddSingleton(receiptOcrService);
            });
        }
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
