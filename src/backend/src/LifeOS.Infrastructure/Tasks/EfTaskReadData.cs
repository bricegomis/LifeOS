using LifeOS.Application.Tasks.Contracts;
using LifeOS.Domain.Tasks;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Tasks;

internal sealed class EfTaskReadData(LifeOSDbContext db) : ITaskReadData
{
    public async Task<IReadOnlyList<Todo>> OpenTodosAsync(Guid householdId, CancellationToken ct) =>
        await db.Todos.AsNoTracking().Where(t => t.HouseholdId == householdId && t.State == "open").ToListAsync(ct);

    public async Task<IReadOnlyList<Todo>> DoneTodosAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.Todos.AsNoTracking()
            .Where(t => t.HouseholdId == householdId && t.State == "done" && t.DoneOn >= from && t.DoneOn <= to)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskRoutine>> RoutinesAsync(Guid householdId, bool activeOnly, CancellationToken ct) =>
        await db.TaskRoutines.AsNoTracking()
            .Where(r => r.HouseholdId == householdId && (!activeOnly || r.State == "active"))
            .ToListAsync(ct);
}
