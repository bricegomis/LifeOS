using LifeOS.Domain.Sports;

namespace LifeOS.Infrastructure.Sports;

/// <summary>The Sport module owns its PostgreSQL schema (ADR 0009, § 2).</summary>
internal static class SportsSchema
{
    public const string Name = "sports";

    /// <summary>SQL list of the sport codes, for check constraints.</summary>
    public static string SportCodes => string.Join(", ", SportCatalog.Sports.Keys.Select(code => $"'{code}'"));
}
