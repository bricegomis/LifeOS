using LifeOS.Domain.Library;

namespace LifeOS.Application.Common.Interfaces;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> GetAllAsync(CancellationToken cancellationToken = default);
}
