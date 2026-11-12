using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Households;

namespace LifeOS.Application.Households;

/// <summary>The identity Google returned after a successful sign-in.</summary>
public sealed record GoogleIdentity(string Subject, string? Email, bool EmailVerified, string? DisplayName);

/// <summary>Which accounts may sign in (ADR 0011): only the listed, verified e-mail addresses.</summary>
public sealed class AccountAccessPolicy(IEnumerable<string> allowedEmails)
{
    private readonly HashSet<string> _allowed = allowedEmails
        .Where(email => !string.IsNullOrWhiteSpace(email))
        .Select(email => email.Trim().ToLowerInvariant())
        .ToHashSet(StringComparer.Ordinal);

    public bool Allows(string? email) =>
        !string.IsNullOrWhiteSpace(email) && _allowed.Contains(email.Trim().ToLowerInvariant());
}

/// <summary>
/// Use case: turns a Google identity into a LifeOS account. Refuses unverified or unlisted
/// addresses; creates the account on first sign-in and refreshes its e-mail and name after.
/// Returns the account id, or <c>null</c> when sign-in is refused.
/// </summary>
public sealed class SignInWithGoogle(IUserAccountRepository accounts, AccountAccessPolicy policy, TimeProvider clock)
{
    public async Task<Guid?> ExecuteAsync(GoogleIdentity identity, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identity.Subject) || !identity.EmailVerified || !policy.Allows(identity.Email))
            return null;

        var now = clock.GetUtcNow();
        var account = await accounts.FindByGoogleSubjectAsync(identity.Subject, cancellationToken);
        var isNew = account is null;
        if (account is null)
            account = UserAccount.Create(identity.Subject, identity.Email!, identity.DisplayName, now);
        else
            account.RecordSignIn(identity.Email!, identity.DisplayName, now);

        await accounts.SaveAsync(account, isNew, cancellationToken);
        return account.Id;
    }
}

/// <summary>
/// Use case: checks at each token issuance (sign-in, refresh) that the account still exists
/// and is still allowed, so removing an address from the list cuts access within one token lifetime.
/// </summary>
public sealed class GetActiveAccount(IUserAccountRepository accounts, AccountAccessPolicy policy)
{
    public async Task<UserAccount?> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByIdAsync(userId, cancellationToken);
        return account is not null && policy.Allows(account.Email) ? account : null;
    }
}
