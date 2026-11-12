using System.Net.Http.Json;
using LifeOS.Api.Food.Foods;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LifeOS.Api.IntegrationTests;

/// <summary>Roadmap step 1 reset (ADR 0010, § 6) applied to a database of the previous models.</summary>
public sealed class ResetMigrationTests
{
    [Fact]
    public async Task The_reset_drops_every_legacy_table_and_the_api_starts_on_the_module_schemas()
    {
        await using var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("reset").WithUsername("reset").WithPassword("reset").Build();
        await container.StartAsync();

        // A database left by the previous models: mapped and unmapped tables with data, and the
        // history rows of migrations that no longer exist in the code.
        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
                INSERT INTO "__EFMigrationsHistory" VALUES ('20260914045103_InitialCreate', '9.0.4'), ('20261111151954_DropRecipeMetadata', '9.0.4');
                CREATE TABLE households ("Id" uuid PRIMARY KEY, "Name" text);
                INSERT INTO households VALUES ('00000000-0000-0000-0000-000000000001', 'Ancien foyer');
                CREATE TABLE foods (id uuid PRIMARY KEY, household_id uuid REFERENCES households ("Id"), name text);
                INSERT INTO foods VALUES (gen_random_uuid(), '00000000-0000-0000-0000-000000000001', 'Riz');
                CREATE TABLE stock_items (id uuid PRIMARY KEY);
                CREATE TABLE shopping_list_items (id uuid PRIMARY KEY);
                """, connection);
            await command.ExecuteNonQueryAsync();
        }

        await using var factory = new LifeOSApiFactory(container.GetConnectionString());
        var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var created = await client.PostAsJsonAsync("/api/foods", new FoodRequest("Lentilles", "100 g", null));
        created.EnsureSuccessStatusCode();
        Assert.Single(await client.GetFromJsonAsync<List<FoodItemDto>>("/api/foods") ?? []);

        await using var db = factory.CreateDbContext();
        var publicTables = await db.Database.SqlQueryRaw<string>(
            "SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'").ToListAsync();
        Assert.Equal(["__EFMigrationsHistory"], publicTables);
        var history = await db.Database.SqlQueryRaw<string>(
            "SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"").ToListAsync();
        Assert.Equal(["20261112055644_FoodModuleReset"], history);
        var schemas = await db.Database.SqlQueryRaw<string>(
            "SELECT DISTINCT schemaname AS \"Value\" FROM pg_tables WHERE schemaname IN ('households', 'food', 'sports') ORDER BY 1").ToListAsync();
        Assert.Equal(["food", "households", "sports"], schemas);
    }
}
