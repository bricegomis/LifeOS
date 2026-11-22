using System.Security.Cryptography;
using LifeOS.Application.Common.Files;

namespace LifeOS.Infrastructure.Files;

/// <summary>
/// Stores private files encrypted at rest (AES-GCM, 256-bit key from the configuration, never in the
/// database): a random 12-byte nonce, the 16-byte tag, then the ciphertext. JPEG, PNG, WebP and, when
/// allowed, PDF; the declared type must match the file signature. Without a key, nothing is stored or
/// served. A lost key makes every file unreadable: it is backed up apart, with the volume.
/// </summary>
public abstract class EncryptedFileStorage
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly string _root;
    private readonly long _maxBytes;
    private readonly byte[]? _key;

    protected EncryptedFileStorage(string storagePath, long maxBytes, string? base64Key)
    {
        _root = Path.GetFullPath(storagePath);
        _maxBytes = maxBytes > 0 ? maxBytes : 20 * 1024 * 1024;
        if (!string.IsNullOrWhiteSpace(base64Key))
        {
            var key = Convert.FromBase64String(base64Key.Trim());
            if (key.Length != 32) throw new InvalidOperationException("The encryption key must be 32 bytes, base64-encoded.");
            _key = key;
        }
    }

    public bool IsConfigured => _key is not null;
    public long MaxBytes => _maxBytes;

    public async Task<StoredFile> SaveAsync(Stream content, long length, string? declaredContentType, bool allowPdf, CancellationToken ct)
    {
        var key = _key ?? throw new ArgumentException("Le stockage chiffré des fichiers de santé n'est pas configuré (clé absente).");
        var limit = $"{_maxBytes / 1024 / 1024} Mo";
        if (length <= 0 || length > _maxBytes) throw new ArgumentException($"Le fichier doit peser entre 1 octet et {limit}.");
        var contentType = (declaredContentType ?? "").ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp") && !(allowPdf && contentType == "application/pdf"))
            throw new ArgumentException(allowPdf ? "Formats acceptés : JPEG, PNG, WebP et PDF." : "Formats acceptés : JPEG, PNG et WebP.");

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > _maxBytes) throw new ArgumentException($"Le fichier dépasse la limite de {limit}.");
            buffer.Write(chunk, 0, read);
        }
        var plain = buffer.ToArray();
        if (!HasValidSignature(contentType, plain)) throw new ArgumentException("Le contenu du fichier ne correspond pas à son type.");

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];
        using (var aes = new AesGcm(key, TagSize)) aes.Encrypt(nonce, plain, cipher, tag);

        Directory.CreateDirectory(_root);
        var fileName = $"{Guid.NewGuid():N}.bin";
        var path = ResolvePath(fileName);
        await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await destination.WriteAsync(nonce, ct);
            await destination.WriteAsync(tag, ct);
            await destination.WriteAsync(cipher, ct);
        }
        return new StoredFile(fileName, contentType, Convert.ToHexString(SHA256.HashData(plain)));
    }

    /// <summary>The decrypted bytes of a stored file.</summary>
    public async Task<byte[]> ReadAsync(string fileName, CancellationToken ct)
    {
        var key = _key ?? throw new InvalidOperationException("Health file storage has no encryption key.");
        var data = await File.ReadAllBytesAsync(ResolvePath(fileName), ct);
        if (data.Length < NonceSize + TagSize) throw new InvalidOperationException("Corrupted encrypted file.");
        var plain = new byte[data.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize + TagSize), data.AsSpan(NonceSize, TagSize), plain);
        return plain;
    }

    public bool Exists(string fileName) => File.Exists(ResolvePath(fileName));

    public void Delete(string fileName)
    {
        var path = ResolvePath(fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteQuietly(string? fileName, Action<Exception> onError)
    {
        if (fileName is null) return;
        try { Delete(fileName); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { onError(exception); }
    }

    private string ResolvePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName) throw new ArgumentException("Référence de fichier invalide.");
        var path = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new ArgumentException("Référence de fichier invalide.");
        return path;
    }

    private static bool HasValidSignature(string contentType, ReadOnlySpan<byte> bytes) => contentType switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        "application/pdf" => bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8),
        _ => false,
    };
}
