using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LifeOS.Api.Receipts;

public sealed class ReceiptPhotoStorageOptions
{
    public string StoragePath { get; set; } = "/data/receipts";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
}

public sealed record StoredReceiptPhoto(string FileName, string ContentType, string Sha256);

public sealed class ReceiptPhotoStorage(IOptions<ReceiptPhotoStorageOptions> options)
{
    public const long DefaultMaxFileBytes = 10 * 1024 * 1024;
    private readonly string _root = Path.GetFullPath(options.Value.StoragePath);

    public async Task<StoredReceiptPhoto> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var maxBytes = options.Value.MaxFileBytes is > 0 and <= 25 * 1024 * 1024
            ? options.Value.MaxFileBytes
            : DefaultMaxFileBytes;
        if (file.Length is <= 0 || file.Length > maxBytes)
            throw new ArgumentException($"La photo doit peser entre 1 octet et {maxBytes / 1024 / 1024} Mo.");

        var contentType = (file.ContentType ?? "").ToLowerInvariant();
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException("Formats acceptés : JPEG, PNG et WebP.")
        };

        var signature = new byte[12];
        await using (var input = file.OpenReadStream())
        {
            var offset = 0;
            while (offset < signature.Length)
            {
                var read = await input.ReadAsync(signature.AsMemory(offset), cancellationToken);
                if (read == 0) break;
                offset += read;
            }
            if (!HasValidSignature(contentType, signature.AsSpan(0, offset)))
                throw new ArgumentException("Le contenu du fichier ne correspond pas à son type d'image.");
        }

        Directory.CreateDirectory(_root);
        var filename = $"{Guid.NewGuid():N}{extension}";
        var path = ResolvePath(filename);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = file.OpenReadStream();
        await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        long total = 0;
        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                total += read;
                if (total > maxBytes) throw new ArgumentException($"La photo dépasse la limite de {maxBytes / 1024 / 1024} Mo.");
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await destination.FlushAsync(cancellationToken);
        }
        catch
        {
            await destination.DisposeAsync();
            File.Delete(path);
            throw;
        }

        return new StoredReceiptPhoto(filename, contentType, Convert.ToHexString(hash.GetHashAndReset()));
    }

    public string ResolvePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Référence de photo invalide.");
        var path = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Référence de photo invalide.");
        return path;
    }

    public void Delete(string fileName)
    {
        var path = ResolvePath(fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public FileStream OpenRead(string fileName) =>
        new(ResolvePath(fileName), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

    private static bool HasValidSignature(string contentType, ReadOnlySpan<byte> bytes) => contentType switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };
}
