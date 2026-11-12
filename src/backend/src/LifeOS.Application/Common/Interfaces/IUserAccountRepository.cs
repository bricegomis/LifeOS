using LifeOS.Domain.Households;

namespace LifeOS.Application.Common.Interfaces;

/// <summary>Persistence port for <see cref="UserAccount"/>. Implemented in Infrastructure.</summary>
public interface IUserAccountRepository
{
    Task<UserAccount?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Adds the account when new, then saves pending changes.</summary>
    Task SaveAsync(UserAccount account, bool isNew, CancellationToken cancellationToken = default);
}
