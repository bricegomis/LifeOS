using LifeOS.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Reading;

public sealed class BookImageStorageOptions
{
    public string StoragePath { get; set; } = "/data/books";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>Book covers (volume <c>lifeos_book_images</c>, ADR 0022).</summary>
public sealed class BookImageStorage(IOptions<BookImageStorageOptions> options)
    : ImageFileStorage(options.Value.StoragePath, options.Value.MaxFileBytes, 10 * 1024 * 1024);
