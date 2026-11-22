using LifeOS.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Tasks;

public sealed class TaskImageStorageOptions
{
    public string StoragePath { get; set; } = "/data/tasks";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>Todo images (volume <c>lifeos_task_images</c>, ADR 0017).</summary>
public sealed class TaskImageStorage(IOptions<TaskImageStorageOptions> options)
    : ImageFileStorage(options.Value.StoragePath, options.Value.MaxFileBytes, 10 * 1024 * 1024);
