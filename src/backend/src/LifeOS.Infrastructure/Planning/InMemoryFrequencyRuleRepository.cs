using System.Collections.Concurrent;
using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Planning;

namespace LifeOS.Infrastructure.Planning;

/// <summary>
/// Temporary in-memory implementation of <see cref="IFrequencyRuleRepository"/>, seeded per owner
/// with the frontend's default frequency rules. This is the first, simplest persistence port; it
/// is expected to be replaced by a real database once the feature grows beyond simple CRUD.
/// </summary>
public sealed class InMemoryFrequencyRuleRepository : IFrequencyRuleRepository
{
    private readonly ConcurrentDictionary<Guid, List<FrequencyRule>> _rulesByOwner = new();

    public Task<IReadOnlyList<FrequencyRule>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var rules = _rulesByOwner.GetOrAdd(ownerId, CreateSeedRules);

        IReadOnlyList<FrequencyRule> result = rules.ToList();
        return Task.FromResult(result);
    }

    public Task<FrequencyRule?> GetByIdAsync(Guid ownerId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rules = _rulesByOwner.GetOrAdd(ownerId, CreateSeedRules);

        return Task.FromResult(rules.Find(rule => rule.Id == ruleId));
    }

    public Task AddAsync(FrequencyRule rule, CancellationToken cancellationToken = default)
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

    private static List<FrequencyRule> CreateSeedRules(Guid ownerId)
    {
        return
        [
            FrequencyRule.Create(ownerId, new FrequencyRuleTarget.Component("sardines"), 3),
            FrequencyRule.Create(ownerId, new FrequencyRuleTarget.Component("tempeh"), 1),
            FrequencyRule.Create(ownerId, new FrequencyRuleTarget.Category("pleasure-meal", "Repas plaisir"), 2),
        ];
    }
}
