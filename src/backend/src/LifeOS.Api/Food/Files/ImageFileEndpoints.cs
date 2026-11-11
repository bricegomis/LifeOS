using LifeOS.Infrastructure.Food.Files;

namespace LifeOS.Api.Food.Files;

/// <summary>HTTP glue for <see cref="ImageFileStorage"/>: multipart upload and private download.</summary>
public static class ImageFileEndpoints
{
    public const long MaxRequestBytes = 11 * 1024 * 1024;

    public static async Task<StoredImage> SaveUploadAsync(this ImageFileStorage storage, IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return await storage.SaveAsync(content, file.Length, file.ContentType, ct);
    }

    public static async Task<IFormFile> ReadFileAsync(this HttpRequest request, string field, string missingMessage, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        return form.Files.GetFile(field) ?? throw new ArgumentException(missingMessage);
    }

    public static IResult Serve(this ImageFileStorage storage, HttpContext context, string? fileName, string? contentType)
    {
        if (fileName is null || contentType is null || !storage.Exists(fileName)) return Results.NotFound();
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.File(storage.OpenRead(fileName), contentType);
    }

    public static RouteHandlerBuilder WithUploadLimit(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxRequestBytes),
            new Microsoft.AspNetCore.Mvc.RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxRequestBytes });
}
