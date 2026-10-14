using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Contracts;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class FoodImagesTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Food_image_is_persisted_replaced_and_served_only_to_its_household()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-food-images-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString, foodImageStoragePath: storagePath);
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());
            using var other = factory.CreateClient().AsUser(Guid.NewGuid());

            using var create = await client.PostAsJsonAsync("/api/foods", new FoodRequest("Carotte", "", null));
            create.EnsureSuccessStatusCode();
            var food = await create.Content.ReadFromJsonAsync<JsonElement>();
            var foodId = food.GetProperty("id").GetGuid();
            Assert.False(food.GetProperty("hasImage").GetBoolean());

            var firstImage = PngBytes(1);
            using var firstUpload = await client.PutAsync($"/api/foods/{foodId}/image", ImageForm(firstImage));
            Assert.Equal(HttpStatusCode.OK, firstUpload.StatusCode);
            food = await firstUpload.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(food.GetProperty("hasImage").GetBoolean());

            using var firstRead = await client.GetAsync($"/api/foods/{foodId}/image");
            Assert.Equal(HttpStatusCode.OK, firstRead.StatusCode);
            Assert.Equal("image/png", firstRead.Content.Headers.ContentType?.MediaType);
            Assert.Equal(firstImage, await firstRead.Content.ReadAsByteArrayAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/foods/{foodId}/image")).StatusCode);

            using var update = await client.PutAsJsonAsync($"/api/foods/{foodId}",
                new FoodRequest("Carotte orange", "", null));
            update.EnsureSuccessStatusCode();
            Assert.True((await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hasImage").GetBoolean());

            var replacement = PngBytes(2);
            using var secondUpload = await client.PutAsync($"/api/foods/{foodId}/image", ImageForm(replacement));
            secondUpload.EnsureSuccessStatusCode();
            Assert.Single(Directory.GetFiles(storagePath));
            using var secondRead = await client.GetAsync($"/api/foods/{foodId}/image");
            Assert.Equal(replacement, await secondRead.Content.ReadAsByteArrayAsync());

            using var remove = await client.DeleteAsync($"/api/foods/{foodId}/image");
            Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
            Assert.Empty(Directory.GetFiles(storagePath));
            using var missing = await client.GetAsync($"/api/foods/{foodId}/image");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var readFood = await client.GetAsync($"/api/foods/{foodId}");
            Assert.False((await readFood.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hasImage").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task Food_image_upload_rejects_mismatched_file_content()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-food-images-invalid-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString, foodImageStoragePath: storagePath);
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());
            using var create = await client.PostAsJsonAsync("/api/foods", new FoodRequest("Poire", "", null));
            create.EnsureSuccessStatusCode();
            var food = await create.Content.ReadFromJsonAsync<JsonElement>();

            using var response = await client.PutAsync(
                $"/api/foods/{food.GetProperty("id").GetGuid()}/image", ImageForm(new byte[12]));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(Directory.Exists(storagePath));
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "food.png");
        return form;
    }

    private static byte[] PngBytes(byte discriminator) =>
        [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, discriminator];
}
