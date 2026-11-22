namespace LifeOS.Infrastructure.Health;

/// <summary>The Health module owns its PostgreSQL schema (ADR 0009, § 2).</summary>
internal static class HealthSchema
{
    public const string Name = "health";
}
