using LifeOS.Application.Sports.Contracts;
using LifeOS.Domain.Sports;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Sports;

internal sealed class EfSportReadData(LifeOSDbContext db) : ISportReadData
{
    public async Task<IReadOnlyList<SportSession>> SessionsAsync(Guid householdId, Guid? profileId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.SportSessions.AsNoTracking()
            .Where(s => s.HouseholdId == householdId && s.Date >= from && s.Date <= to)
            .Where(s => profileId == null || s.MemberProfileId == profileId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WeeklyGoal>> GoalsAsync(Guid householdId, Guid profileId, DateOnly fromMonday, DateOnly toMonday, CancellationToken ct) =>
        await db.WeeklyGoals.AsNoTracking()
            .Where(g => g.HouseholdId == householdId && g.MemberProfileId == profileId)
            .Where(g => g.ValidFrom <= toMonday && (g.ValidUntil == null || g.ValidUntil > fromMonday))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> TemplateNamesAsync(Guid householdId, CancellationToken ct) =>
        await db.SportTemplates.AsNoTracking().Where(t => t.HouseholdId == householdId).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

    public async Task<IReadOnlyList<DailyChallenge>> ChallengesAsync(Guid householdId, Guid profileId, bool activeOnly, CancellationToken ct) =>
        await db.DailyChallenges.AsNoTracking()
            .Where(c => c.HouseholdId == householdId && c.MemberProfileId == profileId)
            .Where(c => !activeOnly || c.StoppedOn == null)
            .OrderBy(c => c.StartedOn).ThenBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, Dictionary<DateOnly, int>>> DayTotalsAsync(Guid householdId, IReadOnlyCollection<Guid> challengeIds, CancellationToken ct)
    {
        var rows = await db.ChallengeEntries.AsNoTracking()
            .Where(e => e.HouseholdId == householdId && challengeIds.Contains(e.ChallengeId))
            .GroupBy(e => new { e.ChallengeId, e.Date })
            .Select(g => new { g.Key.ChallengeId, g.Key.Date, Total = g.Sum(e => e.Quantity) })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.ChallengeId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Date, r => r.Total));
    }

    public async Task<IReadOnlyList<ChallengeEntry>> EntriesAsync(Guid householdId, IReadOnlyCollection<Guid> challengeIds, DateOnly day, CancellationToken ct) =>
        await db.ChallengeEntries.AsNoTracking()
            .Where(e => e.HouseholdId == householdId && challengeIds.Contains(e.ChallengeId) && e.Date == day)
            .ToListAsync(ct);
}
