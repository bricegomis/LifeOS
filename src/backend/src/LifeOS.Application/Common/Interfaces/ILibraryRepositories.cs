using LifeOS.Domain.Library;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Persistence port for the shared, read-only <see cref="MealComponent"/> library.
/// </summary>
public interface IMealComponentRepository
{
    Task<IReadOnlyList<MealComponent>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persistence port for the shared, read-only <see cref="CompositeDish"/> library.
/// </summary>
public interface ICompositeDishRepository
{
    Task<IReadOnlyList<CompositeDish>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persistence port for the shared, read-only <see cref="Activity"/> library.
/// </summary>
public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> GetAllAsync(CancellationToken cancellationToken = default);
}
