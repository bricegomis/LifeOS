using LifeOS.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Sports;

public sealed class SportImageStorageOptions
{
    public string StoragePath { get; set; } = "/data/sports";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>Exercise images (volume <c>lifeos_sport_images</c>, ADR 0014).</summary>
public sealed class SportImageStorage(IOptions<SportImageStorageOptions> options)
    : ImageFileStorage(options.Value.StoragePath, options.Value.MaxFileBytes, 10 * 1024 * 1024);
