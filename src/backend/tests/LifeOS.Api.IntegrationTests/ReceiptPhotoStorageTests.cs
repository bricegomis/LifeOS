using System.Security.Cryptography;
using LifeOS.Api.Receipts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LifeOS.Api.IntegrationTests;

public sealed class ReceiptPhotoStorageTests
{
    [Fact]
    public async Task Save_uses_server_generated_path_and_preserves_original_bytes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lifeos-photo-storage-{Guid.NewGuid():N}");
        try
        {
            var storage = new ReceiptPhotoStorage(Options.Create(new ReceiptPhotoStorageOptions { StoragePath = root }));
            var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 4, 5, 6, 7 };
            var saved = await storage.SaveAsync(FormFile(bytes, "image/png", "../../../../receipt.png"), CancellationToken.None);

            Assert.NotEqual("../../../../receipt.png", saved.FileName);
            Assert.Equal(".png", Path.GetExtension(saved.FileName));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), saved.Sha256);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(storage.ResolvePath(saved.FileName)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_rejects_oversized_and_mismatched_image_content()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lifeos-photo-validation-{Guid.NewGuid():N}");
        try
        {
            var limited = new ReceiptPhotoStorage(Options.Create(new ReceiptPhotoStorageOptions
            {
                StoragePath = root,
                MaxFileBytes = 8,
            }));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                limited.SaveAsync(FormFile(new byte[9], "image/png"), CancellationToken.None));

            var storage = new ReceiptPhotoStorage(Options.Create(new ReceiptPhotoStorageOptions { StoragePath = root }));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                storage.SaveAsync(FormFile(new byte[12], "image/png"), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                storage.SaveAsync(FormFile(new byte[12], "image/gif"), CancellationToken.None));
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static FormFile FormFile(byte[] bytes, string contentType, string filename = "ticket.jpg")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "photo", filename)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }
}
