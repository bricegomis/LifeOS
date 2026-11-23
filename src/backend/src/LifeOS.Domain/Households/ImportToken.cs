using System.Security.Cryptography;
using System.Text;
using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

/// <summary>
/// A secret that lets an import service (garmin-sync, ADR 0019) write for one account and one profile,
/// only through the import endpoints. Only its SHA-256 hash is stored; the token is shown once.
/// </summary>
public sealed class ImportToken : Entity
{
    public const string Prefix = "lifeos_imp_";

    public Guid HouseholdId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Name { get; private set; } = "";
    public string Hash { get; private set; } = "";
    /// <summary>Last characters of the token, to recognise it in the settings.</summary>
    public string Hint { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private ImportToken() { }

    public bool IsActive => RevokedAt is null;

    /// <returns>The token and the secret to show once.</returns>
    public static (ImportToken Token, string Secret) Create(Guid householdId, Guid userId, Guid memberProfileId, string? name, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || userId == Guid.Empty || memberProfileId == Guid.Empty)
            throw new ArgumentException("Foyer, compte et profil obligatoires.");
        var label = string.IsNullOrWhiteSpace(name) ? "garmin-sync" : name.Trim();
        if (label.Length > 100) throw new ArgumentException("Nom : 100 caractères maximum.");
        var secret = Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = new ImportToken
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, UserId = userId, MemberProfileId = memberProfileId,
            Name = label, Hash = HashOf(secret), Hint = secret[^4..], CreatedAt = now,
        };
        return (token, secret);
    }

    public static string HashOf(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Records a use, at most once a minute to spare writes.</summary>
    public bool Touch(DateTimeOffset now)
    {
        if (LastUsedAt is { } last && now - last < TimeSpan.FromMinutes(1)) return false;
        LastUsedAt = now;
        return true;
    }
}
