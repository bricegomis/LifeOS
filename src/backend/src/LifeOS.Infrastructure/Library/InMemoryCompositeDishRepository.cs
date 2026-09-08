using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Library;

namespace LifeOS.Infrastructure.Library;

/// <summary>
/// In-memory implementation of <see cref="ICompositeDishRepository"/>, seeded with the same data
/// as the frontend's <c>src/frontend/src/data/localLibrary.ts</c>. This is a shared, read-only
/// library: it is not scoped per owner.
/// </summary>
public sealed class InMemoryCompositeDishRepository : ICompositeDishRepository
{
    private static readonly IReadOnlyList<CompositeDish> Seed =
    [
        new("lentil-rice-dhal", "Dhal lentilles-riz", "🍛", 620, 32, 86, 15, 25, false, true, true, false, true, true),
        new("tempeh-curry", "Curry de tempeh", "🍲", 680, 38, 70, 24, 30, false, true, true, false, true, true),
        new("homemade-chili", "Chili maison", "🌶️", 700, 42, 78, 22, 35, false, true, true, true, true, true),
        new("protein-pancakes", "Pancakes protéinés", "🥞", 520, 39, 54, 16, 15, true, false, false, true, false, true),
        new("buckwheat-cheese-pasta", "Pâtes de sarrasin au fromage", "🍝", 760, 36, 86, 29, 18, false, true, true, true, false, true),
        new("roast-chicken-vegetables", "Poulet rôti et légumes", "🍗", 690, 52, 48, 26, 45, false, true, true, true, true, true),
        new("lasagna", "Lasagnes", "🍽️", 730, 38, 82, 28, 45, false, true, true, true, true, true),
        new("homemade-pizza", "Pizza maison", "🍕", 780, 34, 92, 28, 40, false, true, true, true, false, true),
    ];

    public Task<IReadOnlyList<CompositeDish>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Seed);
}
