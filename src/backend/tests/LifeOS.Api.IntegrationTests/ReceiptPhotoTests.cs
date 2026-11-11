using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Endpoints;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class ReceiptPhotoTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task A_photo_is_stored_as_a_pending_receipt_and_creates_no_product_or_price()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-receipt-photo-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString, receiptStoragePath: storagePath);
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());
            var bytes = PngBytes(1);

            using var upload = await client.PostAsync("/api/receipts/photo", PhotoForm(bytes));
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            var receipt = (await upload.Content.ReadFromJsonAsync<ReceiptDto>())!;
            Assert.Equal("pending", receipt.Status);
            Assert.Null(receipt.StoreId);
            Assert.Null(receipt.PurchaseDate);
            Assert.Empty((await client.GetFromJsonAsync<List<JsonElement>>("/api/products"))!);
            Assert.Single(Directory.GetFiles(storagePath));

            using var image = await client.GetAsync($"/api/receipts/{receipt.Id}/photo");
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
            Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync());

            // The same bytes are refused for this household and leave no orphan file behind.
            using var duplicate = await client.PostAsync("/api/receipts/photo", PhotoForm(bytes));
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Single(Directory.GetFiles(storagePath));
            Assert.Single((await client.GetFromJsonAsync<List<ReceiptDto>>("/api/receipts"))!);
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task Store_and_date_are_optional_then_editable_and_processing_is_reversible()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var storeId = await CreateStore(client, "Biocoop");

        using var form = PhotoForm(PngBytes(2));
        form.Add(new StringContent(storeId.ToString()), "storeId");
        form.Add(new StringContent("2026-11-08"), "purchaseDate");
        using var upload = await client.PostAsync("/api/receipts/photo", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var receipt = (await upload.Content.ReadFromJsonAsync<ReceiptDto>())!;
        Assert.Equal(storeId, receipt.StoreId);
        Assert.Equal(new DateOnly(2026, 11, 8), receipt.PurchaseDate);

        var processed = await Put(client, receipt.Id, new UpdateReceiptRequest(null, null, true));
        Assert.Equal("processed", processed.Status);
        Assert.Null(processed.StoreId);
        Assert.Null(processed.PurchaseDate);

        var reopened = await Put(client, receipt.Id, new UpdateReceiptRequest(storeId, new DateOnly(2026, 11, 9), false));
        Assert.Equal("pending", reopened.Status);
        Assert.Equal(storeId, reopened.StoreId);
        Assert.Equal(new DateOnly(2026, 11, 9), reopened.PurchaseDate);
    }

    [Fact]
    public async Task Invalid_uploads_are_rejected_without_keeping_a_file()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-receipt-photo-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString, receiptStoragePath: storagePath);
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());

            using var noPhoto = await client.PostAsync("/api/receipts/photo", new MultipartFormDataContent { { new StringContent("x"), "other" } });
            Assert.Equal(HttpStatusCode.BadRequest, noPhoto.StatusCode);

            using var badDate = PhotoForm(PngBytes(3));
            badDate.Add(new StringContent("08/11/2026"), "purchaseDate");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/receipts/photo", badDate)).StatusCode);

            using var unknownStore = PhotoForm(PngBytes(4));
            unknownStore.Add(new StringContent(Guid.NewGuid().ToString()), "storeId");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/receipts/photo", unknownStore)).StatusCode);

            var notAnImage = new MultipartFormDataContent();
            var file = new ByteArrayContent([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            notAnImage.Add(file, "photo", "ticket.png");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/receipts/photo", notAnImage)).StatusCode);

            Assert.False(Directory.Exists(storagePath) && Directory.GetFiles(storagePath).Length > 0);
            Assert.Empty((await client.GetFromJsonAsync<List<ReceiptDto>>("/api/receipts"))!);
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task Receipts_photos_and_stores_are_isolated_by_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var owner = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var bytes = PngBytes(5);

        using var upload = await owner.PostAsync("/api/receipts/photo", PhotoForm(bytes));
        var receipt = (await upload.Content.ReadFromJsonAsync<ReceiptDto>())!;

        Assert.Empty((await other.GetFromJsonAsync<List<ReceiptDto>>("/api/receipts"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/receipts/{receipt.Id}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/receipts/{receipt.Id}",
            new UpdateReceiptRequest(null, null, true))).StatusCode);

        // Another household's store cannot be attached, and the same photo is not a duplicate there.
        var foreignStore = await CreateStore(other, "Leclerc");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/receipts/{receipt.Id}",
            new UpdateReceiptRequest(foreignStore, null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsync("/api/receipts/photo", PhotoForm(bytes))).StatusCode);

        var unchanged = Assert.Single((await owner.GetFromJsonAsync<List<ReceiptDto>>("/api/receipts"))!);
        Assert.Equal("pending", unchanged.Status);
        Assert.Null(unchanged.StoreId);
    }

    [Theory]
    [InlineData("POST", "/api/receipts")]
    [InlineData("GET", "/api/receipts/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/receipts/00000000-0000-0000-0000-000000000001/context")]
    [InlineData("POST", "/api/receipts/00000000-0000-0000-0000-000000000001/lines")]
    [InlineData("POST", "/api/receipts/00000000-0000-0000-0000-000000000001/finalize")]
    public async Task Json_import_lines_and_validation_routes_are_gone(string method, string path)
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { }),
        });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    private static async Task<ReceiptDto> Put(HttpClient client, Guid id, UpdateReceiptRequest request)
    {
        using var response = await client.PutAsJsonAsync($"/api/receipts/{id}", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ReceiptDto>())!;
    }

    private static async Task<Guid> CreateStore(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/stores", new { name, address = "", isOrganic = false, isLocal = false });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent PhotoForm(byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "photo", "ticket.png");
        return form;
    }

    private static byte[] PngBytes(byte discriminator) =>
        [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, discriminator];
}
