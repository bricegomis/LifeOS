using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Planning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Planning;

public sealed class EfFrequencyRuleRepository(LifeOSDbContext dbContext) : IFrequencyRuleRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<FrequencyRule>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var rules = await _dbContext.FrequencyRules
            .AsNoTracking()
            .Where(rule => rule.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        if (rules.Count > 0)
        {
            return rules;
        }

        var seedRules = CreateSeedRules(householdId);
        await _dbContext.FrequencyRules.AddRangeAsync(seedRules, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return seedRules;
    }

    public Task<FrequencyRule?> GetByIdAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FrequencyRules
            .FirstOrDefaultAsync(rule => rule.Id == ruleId && rule.HouseholdId == householdId, cancellationToken);
    }

    public async Task AddAsync(FrequencyRule rule, CancellationToken cancellationToken = default)
    {
        await _dbContext.FrequencyRules.AddAsync(rule, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FrequencyRule rule, CancellationToken cancellationToken = default)
    {
        _dbContext.FrequencyRules.Update(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await GetByIdAsync(householdId, ruleId, cancellationToken);

        if (rule is null)
        {
            return false;
        }

        _dbContext.FrequencyRules.Remove(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static List<FrequencyRule> CreateSeedRules(Guid householdId)
    {
        return
        [
            FrequencyRule.Create(householdId, new FrequencyRuleTarget.Component("sardines"), 3),
            FrequencyRule.Create(householdId, new FrequencyRuleTarget.Component("tempeh"), 1),
            FrequencyRule.Create(householdId, new FrequencyRuleTarget.Category("pleasure-meal", "Repas plaisir"), 2),
        ];
    }
}
