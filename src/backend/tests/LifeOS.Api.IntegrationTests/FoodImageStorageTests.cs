using LifeOS.Api.FoodItems;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LifeOS.Api.IntegrationTests;

public sealed class FoodImageStorageTests
{
    [Fact]
    public async Task Save_uses_a_server_generated_name_and_preserves_the_image()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lifeos-food-image-storage-{Guid.NewGuid():N}");
        try
        {
            var storage = new FoodImageStorage(Options.Create(new FoodImageStorageOptions { StoragePath = root }));
            var bytes = PngBytes(1);
            var saved = await storage.SaveAsync(FormFile(bytes, "image/png", "../../../../food.png"), CancellationToken.None);

            Assert.NotEqual("../../../../food.png", saved.FileName);
            Assert.Equal(".png", Path.GetExtension(saved.FileName));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(storage.ResolvePath(saved.FileName)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_rejects_oversized_or_mismatched_images()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lifeos-food-image-validation-{Guid.NewGuid():N}");
        try
        {
            var limited = new FoodImageStorage(Options.Create(new FoodImageStorageOptions
            {
                StoragePath = root,
                MaxFileBytes = 8,
            }));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                limited.SaveAsync(FormFile(PngBytes(1), "image/png"), CancellationToken.None));

            var storage = new FoodImageStorage(Options.Create(new FoodImageStorageOptions { StoragePath = root }));
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

    private static FormFile FormFile(byte[] bytes, string contentType, string filename = "food.png")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "image", filename)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static byte[] PngBytes(byte discriminator) =>
        [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, discriminator];
}
