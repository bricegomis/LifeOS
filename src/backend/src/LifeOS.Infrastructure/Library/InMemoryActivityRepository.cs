using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Library;

namespace LifeOS.Infrastructure.Library;

/// <summary>
/// In-memory implementation of <see cref="IActivityRepository"/>, seeded with the same data as the
/// frontend's <c>src/frontend/src/data/localLibrary.ts</c>. This is a shared, read-only library:
/// it is not scoped per owner.
/// </summary>
public sealed class InMemoryActivityRepository : IActivityRepository
{
    private static readonly IReadOnlyList<Activity> Seed =
    [
        new("rest", "Repos", "☕", null),
        new("walk", "Marche", "🚶", 30),
        new("running", "Running", "🏃", 35),
        new("long-running", "Running long", "🏃", 55),
        new("strength", "Renforcement", "🏋️", 40),
        new("mobility", "Mobilité", "🧘", 20),
        new("bike", "Vélo", "🚲", 45),
        new("family", "Activité familiale", "👨‍👩‍👧‍👦", 60),
    ];

    public Task<IReadOnlyList<Activity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Seed);
}
