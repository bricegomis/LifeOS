using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// A person who signs in to LifeOS (ADR 0011). Identified by the stable Google subject;
/// the e-mail and display name are refreshed at each sign-in.
/// </summary>
public sealed class UserAccount : Entity
{
    public const int MaxEmailLength = 320;
    public const int MaxDisplayNameLength = 200;

    public string GoogleSubject { get; private set; }
    public string Email { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSignInAt { get; private set; }

    private UserAccount(Guid id, string googleSubject, string email, string? displayName, DateTimeOffset createdAt, DateTimeOffset lastSignInAt)
        : base(id)
    {
        GoogleSubject = googleSubject;
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
        LastSignInAt = lastSignInAt;
    }

    public static UserAccount Create(string googleSubject, string email, string? displayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(googleSubject))
            throw new ArgumentException("A user account needs its Google subject.", nameof(googleSubject));

        return new UserAccount(Guid.NewGuid(), googleSubject.Trim(), NormalizeEmail(email), NormalizeName(displayName), now, now);
    }

    /// <summary>Rehydrates an account from persisted state (or a known id in test setups).</summary>
    public static UserAccount Rehydrate(Guid id, string googleSubject, string email, string? displayName, DateTimeOffset createdAt, DateTimeOffset lastSignInAt) =>
        new(id, googleSubject, NormalizeEmail(email), NormalizeName(displayName), createdAt, lastSignInAt);

    public void RecordSignIn(string email, string? displayName, DateTimeOffset now)
    {
        Email = NormalizeEmail(email);
        DisplayName = NormalizeName(displayName);
        LastSignInAt = now;
    }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("A user account needs an e-mail address.", nameof(email));
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > MaxEmailLength || !normalized.Contains('@'))
            throw new ArgumentException("Invalid e-mail address.", nameof(email));
        return normalized;
    }

    private static string? NormalizeName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length > MaxDisplayNameLength ? trimmed[..MaxDisplayNameLength] : trimmed;
    }
}
