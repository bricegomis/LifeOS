using LifeOS.Application.Food.Receipts;
using LifeOS.Infrastructure.Food.Files;

namespace LifeOS.Api.Food.Files;

/// <summary>HTTP glue for <see cref="ImageFileStorage"/>: multipart upload and private download.</summary>
public static class ImageFileEndpoints
{
    public const long MaxRequestBytes = 11 * 1024 * 1024;

    public static async Task<StoredFile> SaveUploadAsync(this ImageFileStorage storage, IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return await storage.SaveAsync(content, file.Length, file.ContentType, ct);
    }

    public static async Task<IFormFile> ReadFileAsync(this HttpRequest request, string field, string missingMessage, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        return form.Files.GetFile(field) ?? throw new ArgumentException(missingMessage);
    }

    /// <summary>
    /// Stores <paramref name="content"/>, lets <paramref name="apply"/> point the entity to it and
    /// saves; the new file is removed if saving fails, the replaced one once saving succeeded.
    /// </summary>
    public static async Task ReplaceImageAsync(this ImageFileStorage storage, Stream content, long length, string? contentType,
        string? oldFileName, Action<StoredFile> apply, Func<Task> save, ILogger logger, CancellationToken ct)
    {
        var stored = await storage.SaveAsync(content, length, contentType, ct);
        try
        {
            apply(stored);
            await save();
        }
        catch
        {
            storage.Delete(stored.FileName);
            throw;
        }
        storage.DeleteQuietly(oldFileName, e => logger.LogError(e, "Unable to remove a replaced image"));
    }

    /// <summary>
    /// Serves a stored image privately. A replaced image always gets a new file name, used as
    /// ETag: the browser keeps a private copy and revalidates it (304) instead of downloading it again.
    /// </summary>
    public static IResult Serve(this ImageFileStorage storage, HttpContext context, string? fileName, string? contentType)
    {
        if (fileName is null || contentType is null || !storage.Exists(fileName)) return Results.NotFound();
        var etag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{fileName}\"");
        context.Response.Headers.CacheControl = "private, no-cache";
        context.Response.Headers.ETag = etag.ToString();
        if (context.Request.Headers.IfNoneMatch.ToString() == etag.ToString()) return Results.StatusCode(StatusCodes.Status304NotModified);
        return Results.File(storage.OpenRead(fileName), contentType);
    }

    public static RouteHandlerBuilder WithUploadLimit(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxRequestBytes),
            new Microsoft.AspNetCore.Mvc.RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxRequestBytes });
}
