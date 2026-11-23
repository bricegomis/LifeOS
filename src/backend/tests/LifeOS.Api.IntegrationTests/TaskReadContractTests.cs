using System.Net.Http.Json;
using LifeOS.Api.Tasks;
using LifeOS.Application.Tasks.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0017: the published Tasks read contract reads the tasks tables of one household only.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TaskReadContractTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task The_contract_reads_open_todos_routine_dues_and_done_days_of_one_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var clientA = factory.CreateClient().AsUser(Guid.NewGuid());
        using var clientB = factory.CreateClient().AsUser(Guid.NewGuid());
        var today = (await clientA.GetFromJsonAsync<TaskTodayDto>("/api/tasks/today"))!.Today;

        var open = (await (await clientA.PostAsJsonAsync("/api/tasks/todos",
            new TodoRequest(null, "Appeler le plombier", null, today.AddDays(1), 15, "high", null))).Content.ReadFromJsonAsync<TodoDto>())!;
        var done = (await (await clientA.PostAsJsonAsync("/api/tasks/todos",
            new TodoRequest(null, "Payer la cantine", null, today, 5, null, null))).Content.ReadFromJsonAsync<TodoDto>())!;
        (await clientA.PostAsJsonAsync($"/api/tasks/todos/{done.Id}/complete", new TodoCompleteRequest(null))).EnsureSuccessStatusCode();
        var routine = (await (await clientA.PostAsJsonAsync("/api/tasks/routines", new TaskRoutineRequest(null, "Changer les draps", null, 20,
            null, null, new RecurrenceRequest("delay", null, null, null, null, 7), today))).Content.ReadFromJsonAsync<TaskRoutineDetailDto>())!;
        (await clientA.PostAsJsonAsync($"/api/tasks/routines/{routine.Routine.Id}/done", new RoutineDoneRequest(null, null))).EnsureSuccessStatusCode();
        (await clientB.PostAsJsonAsync("/api/tasks/todos", new TodoRequest(null, "Autre foyer", null, today, null, null, null))).EnsureSuccessStatusCode();

        Guid household;
        await using (var db = factory.CreateDbContext())
            household = await db.Todos.Where(t => t.Id == open.Id).Select(t => t.HouseholdId).SingleAsync();
        using var scope = factory.Services.CreateScope();
        var contract = scope.ServiceProvider.GetRequiredService<TaskReadContract>();

        var todo = Assert.Single(await contract.OpenTodosAsync(household, CancellationToken.None));
        Assert.Equal((open.Id, today.AddDays(1), 15, "high"), (todo.Id, todo.DueOn, todo.DurationMinutes, todo.Priority));
        var dues = await contract.RoutineDuesAsync(household, today.AddDays(14), CancellationToken.None);
        Assert.Equal([today.AddDays(7), today.AddDays(14)], dues.Select(d => d.DueOn));
        var day = Assert.Single(await contract.DaysAsync(household, today, today, CancellationToken.None));
        Assert.Equal((25, true), (day.DurationMinutes, day.DurationComplete));
        Assert.Equal([done.Id, routine.Routine.Id], day.Done.Select(d => d.Id));
    }
}
