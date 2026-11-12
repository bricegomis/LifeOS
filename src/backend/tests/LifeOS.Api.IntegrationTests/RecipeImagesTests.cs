using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeOS.Api.Food.Recipes;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class RecipeImagesTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Recipe_image_is_stored_with_food_images_replaced_and_served_only_to_its_household()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-recipe-images-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString, foodImageStoragePath: storagePath);
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());
            using var other = factory.CreateClient().AsUser(Guid.NewGuid());
            using var create = await client.PostAsJsonAsync("/api/recipes", new CreateRecipeRequest("Dhal", 4, 35));
            var recipe = (await create.Content.ReadFromJsonAsync<RecipeDto>())!;
            Assert.False(recipe.HasImage);

            using var upload = await client.PutAsync($"/api/recipes/{recipe.Id}/image", ImageForm([0xFF, 0xD8, 0xFF, 1]));
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            Assert.True((await upload.Content.ReadFromJsonAsync<RecipeDto>())!.HasImage);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/recipes/{recipe.Id}/image")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync($"/api/recipes/{recipe.Id}/image", ImageForm([0xFF, 0xD8, 0xFF, 3]))).StatusCode);

            using var replace = await client.PutAsync($"/api/recipes/{recipe.Id}/image", ImageForm([0xFF, 0xD8, 0xFF, 2]));
            replace.EnsureSuccessStatusCode();
            Assert.Single(Directory.GetFiles(storagePath));
            using var read = await client.GetAsync($"/api/recipes/{recipe.Id}/image");
            Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 2 }, await read.Content.ReadAsByteArrayAsync());
            Assert.True(read.Headers.CacheControl is { Private: true, NoCache: true });
            using var revalidate = new HttpRequestMessage(HttpMethod.Get, $"/api/recipes/{recipe.Id}/image");
            revalidate.Headers.IfNoneMatch.Add(read.Headers.ETag!);
            Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(revalidate)).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/recipes/{recipe.Id}/image")).StatusCode);
            Assert.Empty(Directory.GetFiles(storagePath));
            Assert.False((await client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{recipe.Id}"))!.HasImage);
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    private static MultipartFormDataContent ImageForm(byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "image", "recipe.jpg" } };
    }
}
