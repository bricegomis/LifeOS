using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0015: ticks of a routine day become one pass of that day, so streaks and records stay the same.</summary>
public sealed class SportRoutineSessionsMigrationTests
{
    [Fact]
    public async Task Existing_ticks_become_one_pass_per_routine_and_day_with_every_set_done()
    {
        await using var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("passes").WithUsername("passes").WithPassword("passes").Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<LifeOSDbContext>().UseNpgsql(container.GetConnectionString()).Options;

        await using (var db = new LifeOSDbContext(options))
        {
            // The database as ADR 0014 left it: a routine of two exercises with 2 sets each, done on the 14th,
            // half done on the 15th; the wall sit was ticked at 0 kg once.
            await db.GetService<IMigrator>().MigrateAsync("20261116100043_SportRoutines");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO households.households ("Id", "Name", "CreatedAt", "UpdatedAt")
                VALUES ('00000000-0000-0000-0000-000000000001', 'Foyer', now(), now());
                INSERT INTO households.member_profiles ("Id", "HouseholdId", "DisplayName", "PortionCoefficient", "CreatedAt", "UpdatedAt")
                VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Moi', 1, now(), now());
                INSERT INTO sports.exercises ("Id", "HouseholdId", "Name", "Measure", "WithLoad", "IsArchived") VALUES
                ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'Chaise', 'hold', true, false),
                ('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', 'Pont', 'reps', false, false);
                INSERT INTO sports.routines ("Id", "HouseholdId", "MemberProfileId", "Name", "StartedOn")
                VALUES ('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', 'Tendons', '2026-11-14');
                INSERT INTO sports.routine_versions ("RoutineId", "ValidFrom", "Days", "JokersPerWeek")
                VALUES ('00000000-0000-0000-0000-000000000005', '2026-11-14', 127, 0);
                INSERT INTO sports.routine_lines ("RoutineId", "ValidFrom", "ExerciseId", "HouseholdId", "Position", "Sets", "Target", "LoadKg") VALUES
                ('00000000-0000-0000-0000-000000000005', '2026-11-14', '00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 0, 2, 45, 10),
                ('00000000-0000-0000-0000-000000000005', '2026-11-14', '00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', 1, 2, 12, NULL);
                INSERT INTO sports.exercise_completions ("Id", "HouseholdId", "RoutineId", "ExerciseId", "Date", "Value", "LoadKg", "LoggedAt") VALUES
                (gen_random_uuid(), '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000003', '2026-11-14', 45, 10, '2026-11-14 07:30+00'),
                (gen_random_uuid(), '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000004', '2026-11-14', 12, NULL, '2026-11-14 07:35+00'),
                (gen_random_uuid(), '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000003', '2026-11-15', 50, 0, '2026-11-15 08:00+00');
                """);
            await db.GetService<IMigrator>().MigrateAsync();
        }

        await using var check = new LifeOSDbContext(options);
        var passes = await check.RoutineSessions.OrderBy(s => s.Date).ToListAsync();
        Assert.Equal([new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)], passes.Select(p => p.Date));
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 7, 30, 0, TimeSpan.Zero), passes[0].StartedAt);
        var ticks = await check.ExerciseCompletions.ToListAsync();
        Assert.All(ticks, tick => Assert.Equal(2, tick.SetsDone));
        Assert.Equal(passes[0].Id, ticks.Single(t => t.Date == passes[0].Date && t.Value == 12).SessionId);
        Assert.Null(ticks.Single(t => t.Value == 50).LoadKg);
        Assert.Null((await check.Exercises.SingleAsync(e => e.Name == "Chaise")).DefaultLoadKg);

        var routine = await check.Routines.SingleAsync();
        Assert.True(passes[0].IsFinished(routine, ticks));
        Assert.False(passes[1].IsFinished(routine, ticks));
        var byDay = passes.GroupBy(p => p.Date).ToDictionary(g => g.Key, g => (IReadOnlyList<IReadOnlyDictionary<Guid, int>>)g
            .Select(p => (IReadOnlyDictionary<Guid, int>)ticks.Where(t => t.SessionId == p.Id).ToDictionary(t => t.ExerciseId, t => t.SetsDone)).ToList());
        var progress = RoutineProgress.Compute(routine, byDay, new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 14));
        Assert.Equal(["Done", "InProgress"], progress.Calendar.Select(day => day.Status.ToString()));
        Assert.Equal((1, 1), (progress.Streak, progress.Record));
    }
}
