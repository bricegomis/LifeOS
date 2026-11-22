namespace LifeOS.Infrastructure.Tasks;

/// <summary>The Tasks module owns its PostgreSQL schema (ADR 0009, § 2).</summary>
internal static class TasksSchema
{
    public const string Name = "tasks";
}
