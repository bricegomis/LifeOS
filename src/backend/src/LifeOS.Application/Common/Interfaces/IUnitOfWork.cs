namespace LifeOS.Application.Common.Interfaces;

/// <summary>
/// Unit of work interface for transaction coordination.
/// Encapsulates SaveChangesAsync to allow transaction management across repositories.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
