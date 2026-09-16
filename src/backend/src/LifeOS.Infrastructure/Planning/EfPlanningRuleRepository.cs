using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Common;
using LifeOS.Domain.Library;
using LifeOS.Domain.Planning;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Planning;

public sealed class EfPlanningRuleRepository(LifeOSDbContext dbContext) : IPlanningRuleRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<PlanningRule>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var rules = await _dbContext.PlanningRules
            .AsNoTracking()
            .Where(rule => rule.HouseholdId == householdId)
            .OrderBy(rule => rule.Weekday)
            .ThenBy(rule => rule.MealType)
            .ToListAsync(cancellationToken);

        if (rules.Count > 0)
        {
            return rules;
        }

        var seedRules = CreateSeedRules(householdId);
        await _dbContext.PlanningRules.AddRangeAsync(seedRules, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return seedRules;
    }

    public Task<PlanningRule?> GetByIdAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanningRules
            .FirstOrDefaultAsync(rule => rule.Id == ruleId && rule.HouseholdId == householdId, cancellationToken);
    }

    public async Task AddAsync(PlanningRule rule, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlanningRules.AddAsync(rule, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlanningRule rule, CancellationToken cancellationToken = default)
    {
        _dbContext.PlanningRules.Update(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid householdId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await GetByIdAsync(householdId, ruleId, cancellationToken);

        if (rule is null)
        {
            return false;
        }

        _dbContext.PlanningRules.Remove(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static List<PlanningRule> CreateSeedRules(Guid householdId)
    {
        return
        [
            PlanningRule.Create(householdId, Weekday.Tuesday, MealType.Lunch, new PlanningRuleTarget.Component("sardines", ComponentType.Protein)),
            PlanningRule.Create(householdId, Weekday.Wednesday, MealType.Lunch, new PlanningRuleTarget.Component("liver", ComponentType.Protein)),
            PlanningRule.Create(householdId, Weekday.Thursday, MealType.Lunch, new PlanningRuleTarget.Component("sardines", ComponentType.Protein)),
            PlanningRule.Create(householdId, Weekday.Saturday, MealType.Dinner, new PlanningRuleTarget.Component("chicken", ComponentType.Protein)),
            PlanningRule.Create(householdId, Weekday.Sunday, MealType.Dinner, new PlanningRuleTarget.Dish("homemade-pizza")),
        ];
    }
}
