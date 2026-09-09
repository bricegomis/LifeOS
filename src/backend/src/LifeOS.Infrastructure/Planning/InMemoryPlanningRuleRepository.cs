using System.Collections.Concurrent;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Common;
using LifeOS.Domain.Library;
using LifeOS.Domain.Planning;

namespace LifeOS.Infrastructure.Planning;

/// <summary>
/// Temporary in-memory implementation of <see cref="IPlanningRuleRepository"/>, seeded per owner
/// with the frontend's default planning rules. This is the first, simplest persistence port; it
/// is expected to be replaced by a real database once the feature grows beyond simple CRUD.
/// </summary>
public sealed class InMemoryPlanningRuleRepository : IPlanningRuleRepository
{
    private readonly ConcurrentDictionary<Guid, List<PlanningRule>> _rulesByOwner = new();

    public Task<IReadOnlyList<PlanningRule>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var rules = _rulesByOwner.GetOrAdd(ownerId, CreateSeedRules);

        IReadOnlyList<PlanningRule> result = rules.ToList();
        return Task.FromResult(result);
    }

    public Task<PlanningRule?> GetByIdAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rules = _rulesByOwner.GetOrAdd(ownerId, CreateSeedRules);

        return Task.FromResult(rules.Find(rule => rule.Id == ruleId));
    }

    public Task AddAsync(PlanningRule rule, CancellationToken cancellationToken = default)
    {
        var rules = _rulesByOwner.GetOrAdd(rule.OwnerId, CreateSeedRules);

        lock (rules)
        {
            rules.Add(rule);
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rules = _rulesByOwner.GetOrAdd(ownerId, CreateSeedRules);

        lock (rules)
        {
            return Task.FromResult(rules.RemoveAll(rule => rule.Id == ruleId) > 0);
        }
    }

    private static List<PlanningRule> CreateSeedRules(Guid ownerId)
    {
        return
        [
            PlanningRule.Create(ownerId, Weekday.Tuesday, MealType.Lunch, new PlanningRuleTarget.Component("sardines", ComponentType.Protein)),
            PlanningRule.Create(ownerId, Weekday.Wednesday, MealType.Lunch, new PlanningRuleTarget.Component("liver", ComponentType.Protein)),
            PlanningRule.Create(ownerId, Weekday.Thursday, MealType.Lunch, new PlanningRuleTarget.Component("sardines", ComponentType.Protein)),
            PlanningRule.Create(ownerId, Weekday.Saturday, MealType.Dinner, new PlanningRuleTarget.Component("chicken", ComponentType.Protein)),
            PlanningRule.Create(ownerId, Weekday.Sunday, MealType.Dinner, new PlanningRuleTarget.Dish("homemade-pizza")),
        ];
    }
}
