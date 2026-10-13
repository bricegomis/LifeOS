using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Endpoints;
using LifeOS.Application.Receipts;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class ReceiptPhotoTests(PostgresContainerFixture postgres)
{
    private const string MultiLineOcr = """
        MARCHÉ DU CENTRE
        13/10/2026 18:42
        2 x YAOURT NATURE 3,50
        TOMATES 2,40
        """;

    [Fact]
    public async Task Photo_draft_keeps_original_until_atomic_confirmation_and_records_unit_price_once()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-receipt-photo-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString,
                receiptStoragePath: storagePath, receiptOcrService: new FixedReceiptOcrService(MultiLineOcr));
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());
            var bytes = PngBytes(1);
            using var form = PhotoForm(bytes);
            using var upload = await client.PostAsync("/api/receipts/photo", form);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            var receipt = await upload.Content.ReadFromJsonAsync<JsonElement>();
            var receiptId = Id(receipt);
            Assert.Equal("draft", receipt.GetProperty("status").GetString());
            Assert.True(receipt.GetProperty("hasPhoto").GetBoolean());
            Assert.Equal(MultiLineOcr, receipt.GetProperty("ocrText").GetString());
            Assert.Equal(2, receipt.GetProperty("lines").GetArrayLength());
            Assert.Equal("MARCHÉ DU CENTRE", receipt.GetProperty("storeName").GetString());
            Assert.Equal("2026-10-13T18:42:00+00:00", receipt.GetProperty("purchasedAt").GetString());
            Assert.Empty((await client.GetFromJsonAsync<List<JsonElement>>("/api/products"))!);

            using var imageResponse = await client.GetAsync($"/api/receipts/{receiptId}/photo");
            Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
            Assert.Equal(bytes, await imageResponse.Content.ReadAsByteArrayAsync());
            using var other = factory.CreateClient().AsUser(Guid.NewGuid());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/receipts/{receiptId}/photo")).StatusCode);

            var lines = receipt.GetProperty("lines").EnumerateArray().ToArray();
            var lineId = Id(lines[0]);
            var context = new ReceiptContextRequest(null, "Marché du Centre", "13/10/2026",
                new DateTimeOffset(2026, 10, 13, 18, 42, 0, TimeSpan.Zero));
            receipt = await Send(client, HttpMethod.Put, $"/api/receipts/{receiptId}/context", context);
            await Send(client, HttpMethod.Put, $"/api/receipts/{receiptId}/lines/{lineId}",
                new CorrectReceiptLineRequest("Yaourt nature", 4m, 2m, "unit", null, false));
            await Send(client, HttpMethod.Post, $"/api/receipts/{receiptId}/lines/{Id(lines[1])}/ignore");

            var finalize = new FinalizeReceiptRequest([
                new FinalizeReceiptLineRequest(lineId, new ValidateReceiptLineRequest(null, null,
                    "Yaourt", "Yaourt nature", null, null, false, false))
            ]);
            var requests = await Task.WhenAll(Enumerable.Range(0, 3)
                .Select(_ => client.PostAsJsonAsync($"/api/receipts/{receiptId}/finalize", finalize)));
            Assert.All(requests, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            receipt = await Send(client, HttpMethod.Get, $"/api/receipts/{receiptId}");
            Assert.Equal("validated", receipt.GetProperty("status").GetString());
            Assert.Equal("validated", receipt.GetProperty("lines")[0].GetProperty("status").GetString());

            var products = await client.GetFromJsonAsync<List<JsonElement>>("/api/products");
            var product = Assert.Single(products!);
            var price = Assert.Single(product.GetProperty("priceHistory").EnumerateArray());
            Assert.Equal(2m, price.GetProperty("price").GetDecimal());
            Assert.Equal(new DateTimeOffset(2026, 10, 13, 18, 42, 0, TimeSpan.Zero),
                price.GetProperty("observedAt").GetDateTimeOffset());
            await using var db = factory.CreateDbContext();
            var purchase = Assert.Single(await db.Purchases.Where(p => p.ReceiptLineId == lineId).ToListAsync());
            Assert.Equal(4m, purchase.Total);
            Assert.Equal(2m, purchase.Quantity);
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_batch_does_not_persist_partial_products_or_validate_receipt()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-receipt-rollback-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString,
                receiptStoragePath: storagePath, receiptOcrService: new FixedReceiptOcrService(MultiLineOcr));
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());
            using var form = PhotoForm(PngBytes(2));
            using var upload = await client.PostAsync("/api/receipts/photo", form);
            var receipt = await upload.Content.ReadFromJsonAsync<JsonElement>();
            var receiptId = Id(receipt);
            var lines = receipt.GetProperty("lines").EnumerateArray().ToArray();
            var firstId = Id(lines[0]);
            var secondId = Id(lines[1]);
            await Send(client, HttpMethod.Put, $"/api/receipts/{receiptId}/context",
                new ReceiptContextRequest(null, "Marché du Centre", "13/10/2026",
                    new DateTimeOffset(2026, 10, 13, 18, 42, 0, TimeSpan.Zero)));
            await Send(client, HttpMethod.Put, $"/api/receipts/{receiptId}/lines/{firstId}",
                new CorrectReceiptLineRequest("Yaourt nature", 4m, 2m, "unit", null, false));
            await Send(client, HttpMethod.Put, $"/api/receipts/{receiptId}/lines/{secondId}",
                new CorrectReceiptLineRequest("Tomates", 2.4m, 1m, "kg", null, false));

            var body = new FinalizeReceiptRequest([
                new FinalizeReceiptLineRequest(firstId, new ValidateReceiptLineRequest(null, null,
                    "Yaourt", "Yaourt nature", null, null, false, false)),
                new FinalizeReceiptLineRequest(secondId, new ValidateReceiptLineRequest(Guid.NewGuid(), null,
                    null, null, null, null, false, false)),
            ]);
            var response = await client.PostAsJsonAsync($"/api/receipts/{receiptId}/finalize", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            receipt = await Send(client, HttpMethod.Get, $"/api/receipts/{receiptId}");
            Assert.Equal("draft", receipt.GetProperty("status").GetString());
            Assert.All(receipt.GetProperty("lines").EnumerateArray(), line =>
                Assert.Equal("pending", line.GetProperty("status").GetString()));
            Assert.Empty((await client.GetFromJsonAsync<List<JsonElement>>("/api/products"))!);
            await using var db = factory.CreateDbContext();
            Assert.Empty(await db.Purchases.Where(p => p.HouseholdId == db.Receipts
                .Where(r => r.Id == receiptId).Select(r => r.HouseholdId).Single()).ToListAsync());
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task Ocr_failure_retains_photo_and_creates_an_empty_editable_draft()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-receipt-ocr-error-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString,
                receiptStoragePath: storagePath, receiptOcrService: new FailingReceiptOcrService());
            using var client = factory.CreateClient().AsUser(Guid.NewGuid());
            using var form = PhotoForm(PngBytes(3));
            using var response = await client.PostAsync("/api/receipts/photo", form);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
            var receiptId = Id(receipt);
            Assert.Equal("draft", receipt.GetProperty("status").GetString());
            Assert.Contains("Tesseract absent", receipt.GetProperty("ocrError").GetString());
            Assert.Empty(receipt.GetProperty("lines").EnumerateArray());
            Assert.True(receipt.GetProperty("hasPhoto").GetBoolean());
            await Send(client, HttpMethod.Post, $"/api/receipts/{receiptId}/lines", new AddReceiptLineRequest("Saisie manuelle"));
            var resumed = await Send(client, HttpMethod.Get, $"/api/receipts/{receiptId}");
            Assert.Equal(1, resumed.GetProperty("lines").GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    private static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        using var response = await client.SendAsync(new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Guid Id(JsonElement value) => value.GetProperty("id").GetGuid();

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

    private sealed class FixedReceiptOcrService(string text) : IReceiptOcrService
    {
        public Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken) => Task.FromResult(text);
    }

    private sealed class FailingReceiptOcrService : IReceiptOcrService
    {
        public Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken) =>
            Task.FromException<string>(new IOException("Tesseract absent"));
    }
}
