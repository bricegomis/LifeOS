using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LifeOS.Infrastructure.Persistence;

/// <summary>
/// Resolves the PostgreSQL connection string from configuration (ADR 0001): the connection
/// string is never committed to the repository and must be supplied out-of-band, either as a
/// standard Npgsql connection string (<c>ConnectionStrings:Postgres</c> /
/// <c>ConnectionStrings__Postgres</c>) or as a <c>DATABASE_URL</c> environment variable in the
/// <c>postgres://user:password@host:port/database</c> form commonly provided by hosting
/// platforms such as Coolify.
/// </summary>
public static class PostgresConnectionStringResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var explicitConnectionString = configuration.GetConnectionString("Postgres");

        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var databaseUrl = configuration["DATABASE_URL"] ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return ConvertDatabaseUrlToConnectionString(databaseUrl);
        }

        throw new InvalidOperationException(
            "No PostgreSQL connection string configured. Set 'ConnectionStrings:Postgres' " +
            "(env var 'ConnectionStrings__Postgres') or 'DATABASE_URL'.");
    }

    private static string ConvertDatabaseUrlToConnectionString(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
        };

        if (userInfo.Length > 0 && !string.IsNullOrEmpty(userInfo[0]))
        {
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        return builder.ConnectionString;
    }
}
