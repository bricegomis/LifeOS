using Testcontainers.PostgreSql;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Starts a single ephemeral PostgreSQL container shared by every test in the
/// <see cref="PostgresCollection"/>. Kept separate from <see cref="LifeOSApiFactory"/> so tests can
/// build and dispose multiple independent API hosts against the *same* database — the only way to
/// prove that data survives a logical API restart (see <see cref="PersistenceAcrossRestartTests"/>).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("lifeos_test")
        .WithUsername("lifeos_test")
        .WithPassword("lifeos_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres";
}
