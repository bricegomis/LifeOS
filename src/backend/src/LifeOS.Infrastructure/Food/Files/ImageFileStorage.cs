using LifeOS.Application.Food.Receipts;
using LifeOS.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Food.Files;

public sealed class FoodImageStorageOptions
{
    public string StoragePath { get; set; } = "/data/foods";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
}

public sealed class ReceiptPhotoStorageOptions
{
    public string StoragePath { get; set; } = "/data/receipts";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>Food and recipe images (volume <c>lifeos_food_images</c>).</summary>
public sealed class FoodImageStorage(IOptions<FoodImageStorageOptions> options)
    : ImageFileStorage(options.Value.StoragePath, options.Value.MaxFileBytes, 10 * 1024 * 1024);

/// <summary>Receipt photos (volume <c>lifeos_receipts</c>).</summary>
public sealed class ReceiptPhotoStorage(IOptions<ReceiptPhotoStorageOptions> options)
    : ImageFileStorage(options.Value.StoragePath, options.Value.MaxFileBytes, 25 * 1024 * 1024), IReceiptPhotoFiles;
