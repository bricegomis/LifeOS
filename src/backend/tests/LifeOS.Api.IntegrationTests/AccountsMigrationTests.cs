using System.Net.Http.Json;
using LifeOS.Api.Food.Foods;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0011: accounts move from Supabase to LifeOS; households are reset once.</summary>
public sealed class AccountsMigrationTests
{
    [Fact]
    public async Task Households_of_Supabase_users_are_reset_and_the_api_starts_on_LifeOS_accounts()
    {
        await using var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("accounts").WithUsername("accounts").WithPassword("accounts").Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<LifeOSDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        await using (var db = new LifeOSDbContext(options))
        {
            // The database as step 1 left it: a Supabase user's household with a store.
            await db.GetService<IMigrator>().MigrateAsync("20261112055644_FoodModuleReset");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO households.households ("Id", "Name", "CreatedAt", "UpdatedAt")
                VALUES ('00000000-0000-0000-0000-000000000001', 'Ancien foyer', now(), now());
                INSERT INTO households.household_members ("Id", "HouseholdId", "SupabaseUserId", "Role", "CreatedAt")
                VALUES (gen_random_uuid(), '00000000-0000-0000-0000-000000000001', gen_random_uuid(), 'Owner', now());
                INSERT INTO food.stores ("Id", "HouseholdId", "Name", "Address", "IsOrganic", "IsLocal", "CreatedAt", "UpdatedAt")
                VALUES (gen_random_uuid(), '00000000-0000-0000-0000-000000000001', 'Biocoop', '', true, false, now(), now());
                """);
        }

        await using var factory = new LifeOSApiFactory(container.GetConnectionString());
        var client = factory.CreateClient().AsUser(Guid.NewGuid());
        (await client.PostAsJsonAsync("/api/foods", new FoodRequest("Lentilles", "100 g", null, Family: "legume"))).EnsureSuccessStatusCode();

        await using var check = factory.CreateDbContext();
        Assert.Equal(1, await check.Households.CountAsync());
        Assert.Equal(0, await check.Stores.CountAsync());
        Assert.Equal(1, await check.UserAccounts.CountAsync());
        var member = await check.HouseholdMembers.SingleAsync();
        Assert.Contains(await check.UserAccounts.Select(user => user.Id).ToListAsync(), id => id == member.UserId);
    }
}
