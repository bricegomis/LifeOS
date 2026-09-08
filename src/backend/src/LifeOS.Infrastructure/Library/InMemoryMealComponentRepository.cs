using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Library;

namespace LifeOS.Infrastructure.Library;

/// <summary>
/// In-memory implementation of <see cref="IMealComponentRepository"/>, seeded with the same data
/// as the frontend's <c>src/frontend/src/data/localLibrary.ts</c>. This is a shared, read-only
/// library: it is not scoped per owner.
/// </summary>
public sealed class InMemoryMealComponentRepository : IMealComponentRepository
{
    private static readonly IReadOnlyList<MealComponent> Seed =
    [
        new("eggs", "Œufs", "🥚", ComponentType.Protein, 210, 18, 1, 15, 3, "pièces", true),
        new("steak", "Steak", "🥩", ComponentType.Protein, 280, 36, 0, 14, 150, "g", true),
        new("sardines", "Sardines", "🐟", ComponentType.Protein, 230, 28, 0, 12, 120, "g", true),
        new("liver", "Foie", "🥩", ComponentType.Protein, 210, 30, 4, 7, 140, "g", true),
        new("chicken", "Poulet", "🍗", ComponentType.Protein, 240, 36, 0, 9, 150, "g", true),
        new("tempeh", "Tempeh", "🫘", ComponentType.Protein, 250, 25, 12, 11, 130, "g", true),
        new("rice", "Riz", "🍚", ComponentType.Starch, 250, 5, 55, 1, 190, "g cuit", true),
        new("millet", "Millet", "🌾", ComponentType.Starch, 220, 6, 44, 2, 170, "g cuit", true),
        new("quinoa", "Quinoa", "🍚", ComponentType.Starch, 230, 8, 40, 4, 180, "g cuit", true),
        new("buckwheat-pasta", "Pâtes 100 % sarrasin", "🍝", ComponentType.Starch, 310, 11, 62, 2, 90, "g sec", true),
        new("buckwheat-couscous", "Couscous de sarrasin", "🥣", ComponentType.Starch, 260, 9, 52, 2, 85, "g sec", true),
        new("broccoli", "Brocolis", "🥦", ComponentType.Vegetable, 70, 5, 10, 1, 200, "g", true),
        new("zucchini", "Courgettes", "🥒", ComponentType.Vegetable, 45, 3, 7, 1, 220, "g", true),
        new("carrots", "Carottes", "🥕", ComponentType.Vegetable, 80, 2, 17, 0, 200, "g", true),
        new("green-beans", "Haricots verts", "🥬", ComponentType.Vegetable, 70, 4, 12, 1, 220, "g", true),
        new("peppers", "Poivrons", "🫑", ComponentType.Vegetable, 65, 2, 13, 0, 200, "g", true),
        new("yogurt-sauce", "Sauce yaourt", "🥣", ComponentType.Optional, 70, 4, 5, 4, 60, "g", true),
        new("cheese", "Fromage", "🧀", ComponentType.Optional, 120, 8, 1, 10, 30, "g", true),
        new("ketchup", "Ketchup", "🍅", ComponentType.Optional, 35, 0, 8, 0, 25, "g", true),
        new("fruit", "Fruit", "🍎", ComponentType.Optional, 90, 1, 22, 0, 1, "portion", true),
    ];

    public Task<IReadOnlyList<MealComponent>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Seed);
}
