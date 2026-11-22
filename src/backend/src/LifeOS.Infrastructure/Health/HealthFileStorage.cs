using LifeOS.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Health;

public sealed class HealthFileStorageOptions
{
    public string StoragePath { get; set; } = "/data/health";
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;
    /// <summary>32-byte AES key, base64; backed up apart from the volume. Without it, health files are refused.</summary>
    public string? EncryptionKey { get; set; }
}

/// <summary>Health files, encrypted at rest (volume <c>lifeos_health_files</c>, ADR 0018).</summary>
public sealed class HealthFileStorage(IOptions<HealthFileStorageOptions> options)
    : EncryptedFileStorage(options.Value.StoragePath, options.Value.MaxFileBytes, options.Value.EncryptionKey);
