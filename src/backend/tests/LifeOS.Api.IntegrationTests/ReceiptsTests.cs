using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Endpoints;
using LifeOS.Application.FoodItems;
using LifeOS.Domain.Products;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class ReceiptsTests(PostgresContainerFixture postgres)
{
    private static string Example => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "receipt.json"));
    private static async Task<JsonElement> Send(HttpClient c, HttpMethod method, string path, object? body = null)
    {
        var response = await c.SendAsync(new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static Guid Id(JsonElement r) => r.GetProperty("id").GetGuid();
    private static Guid Line(JsonElement r, int position) => Id(r.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("position").GetInt32() == position));
    private static object NewProduct(string name, string? food = null, Guid? foodId = null) =>
        new ValidateReceiptLineRequest(null, foodId, food, name, null, null, false, false);

    [Fact]
    public async Task Example_import_preserves_23_sources_without_catalog_writes_and_resume_validates_each_line_once()
    {
        var user = Guid.NewGuid(); Guid receiptId;
        await using (var factory = new LifeOSApiFactory(postgres.ConnectionString))
        {
            using var c = factory.CreateClient().AsUser(user);
            var r = await Send(c, HttpMethod.Post, "/api/receipts", new ImportReceiptRequest(Example));
            receiptId = Id(r);
            Assert.Equal("2026-04-11", r.GetProperty("date").GetString());
            Assert.Equal(23, r.GetProperty("lines").GetArrayLength());
            Assert.Equal(Example, r.GetProperty("rawDocument").GetString());
            Assert.Equal(2, r.GetProperty("lines").EnumerateArray().Count(l => l.GetProperty("excludedMention").GetBoolean()));
            Assert.All(r.GetProperty("lines").EnumerateArray(), l => Assert.Equal("pending", l.GetProperty("status").GetString()));
            Assert.Empty((await c.GetFromJsonAsync<List<FoodItemDto>>("/api/foods"))!);
            Assert.Empty((await c.GetFromJsonAsync<List<JsonElement>>("/api/products"))!);
            await using var db = factory.CreateDbContext();
            Assert.Empty(await db.Purchases.Where(p => db.ReceiptLines.Any(l => l.Id == p.ReceiptLineId && l.ReceiptId == receiptId)).ToListAsync());
            var duplicate = await c.PostAsJsonAsync("/api/receipts", new ImportReceiptRequest(Example));
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal(receiptId, (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("receiptId").GetGuid());
            using var other = factory.CreateClient().AsUser(Guid.NewGuid());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/receipts/{receiptId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/receipts/{receiptId}/lines/{Line(r, 0)}/ignore", new { })).StatusCode);
            // Identical documents in different households remain independent.
            var foreign = await Send(other, HttpMethod.Post, "/api/receipts", new ImportReceiptRequest(Example));
            Assert.NotEqual(receiptId, Id(foreign));
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/receipts/{receiptId}/lines/{Line(r, 0)}/validate", NewProduct("Produit", "Aliment"))).StatusCode);
        }
        await using var restart = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = restart.CreateClient().AsUser(user);
        var resumed = await Send(client, HttpMethod.Get, $"/api/receipts/{receiptId}");
        resumed = await Send(client, HttpMethod.Put, $"/api/receipts/{receiptId}/context", new ReceiptContextRequest(null, "Biocoop confirmé", "11/04/2026"));
        var firstLine = Line(resumed, 4);
        var validation = NewProduct("Sardines tartare", "Sardines");
        // Real concurrent double-click: only one product, food, purchase and observation.
        var requests = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync($"/api/receipts/{receiptId}/lines/{firstLine}/validate", validation)));
        Assert.All(requests, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        resumed = await Send(client, HttpMethod.Get, $"/api/receipts/{receiptId}");
        var productId = resumed.GetProperty("lines").EnumerateArray().Single(l => Id(l) == firstLine).GetProperty("productId").GetGuid();
        var food = Assert.Single((await client.GetFromJsonAsync<List<FoodItemDto>>("/api/foods"))!);
        Assert.Null(food.Nutrition); Assert.Null(food.Fodmap); Assert.Null(food.Nova); Assert.Null(food.DigestiveNotes);
        Assert.Equal(7, food.MissingFields.Count);
        resumed = await Send(client, HttpMethod.Post, $"/api/receipts/{receiptId}/lines/{Line(resumed, 5)}/validate",
            new ValidateReceiptLineRequest(productId, null, null, null, null, null, false, false));
        resumed = await Send(client, HttpMethod.Post, $"/api/receipts/{receiptId}/lines/{Line(resumed, 21)}/ignore");
        Assert.Equal(3, resumed.GetProperty("completedLines").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/receipts/{receiptId}/lines/{firstLine}", new CorrectReceiptLineRequest("Autre", 0, 1, "box", null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/receipts/{receiptId}/context", new ReceiptContextRequest(null, "Autre", "12/04/2026"))).StatusCode);
        await using var verify = restart.CreateDbContext();
        Assert.Equal(2, await verify.Purchases.CountAsync(p => p.ProductId == productId));
        Assert.Equal(2, (await verify.Products.SingleAsync(p => p.Id == productId)).PriceHistory.Count);
        Assert.Single(await verify.Products.Where(p => p.HouseholdId == food.HouseholdId).ToListAsync());
    }

    [Fact]
    public async Task Totals_units_and_confirmed_packages_produce_explicit_observations()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var r = await Send(client, HttpMethod.Post, "/api/receipts", new ImportReceiptRequest(Example));
        var id = Id(r);
        r = await Send(client, HttpMethod.Put, $"/api/receipts/{id}/context", new ReceiptContextRequest(null, "Magasin unités", "11/04/2026"));
        await Send(client, HttpMethod.Post, $"/api/receipts/{id}/lines/{Line(r, 11)}/validate", NewProduct("Orange", "Orange"));
        await Send(client, HttpMethod.Post, $"/api/receipts/{id}/lines/{Line(r, 13)}/validate", NewProduct("Avocat", "Avocat"));
        await Send(client, HttpMethod.Put, $"/api/receipts/{id}/lines/{Line(r, 12)}",
            new CorrectReceiptLineRequest("Emmental", 5.95m, 1m, "bag", null, false));
        await Send(client, HttpMethod.Post, $"/api/receipts/{id}/lines/{Line(r, 12)}/validate",
            new ValidateReceiptLineRequest(null, null, "Emmental", "Emmental 400g", 400m, "g", true, false));
        await Send(client, HttpMethod.Post, $"/api/receipts/{id}/lines/{Line(r, 3)}/validate", NewProduct("Haricots boîte", "Haricots"));
        var products = (await client.GetFromJsonAsync<List<JsonElement>>("/api/products"))!;
        decimal Price(string name) => products.Single(p => p.GetProperty("name").GetString() == name).GetProperty("priceHistory")[0].GetProperty("price").GetDecimal();
        Assert.Equal(2.80m, Price("Orange")); Assert.Equal(1.95m, Price("Avocat"));
        Assert.Equal(5.95m, Price("Emmental 400g")); Assert.Equal(2.70m, Price("Haricots boîte"));
        var minima = (await client.GetFromJsonAsync<List<JsonElement>>("/api/foods/prices"))!;
        var prices = minima.SelectMany(m => m.GetProperty("prices").EnumerateArray()).ToList();
        Assert.Contains(prices, p => p.GetProperty("price").GetDecimal() == 14.875m && p.GetProperty("unit").GetString() == "kg");
        Assert.DoesNotContain(prices, p => p.GetProperty("productName").GetString() == "Haricots boîte");
        Assert.DoesNotContain(prices, p => p.GetProperty("productName").GetString() == "Avocat");
    }

    [Fact]
    public async Task Invalid_json_incomplete_lines_mismatches_foreign_links_and_failed_transactions_are_explicit()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/receipts", new ImportReceiptRequest("{broken"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/receipts", new ImportReceiptRequest("""{"aliments":[{},2]}"""))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<JsonElement>>("/api/receipts"))!);
        var r = await Send(client, HttpMethod.Post, "/api/receipts", new ImportReceiptRequest("""{"date":"31/02/2026","aliments":[{"libelle":"Incomplet"},{"libelle":"Écart","prix_achat":10,"quantite":2,"unite":"kg","prix_quantite":3}]}"""));
        Assert.Equal(2, r.GetProperty("lines").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("date").ValueKind);
        var id = Id(r); var incomplete = Line(r, 0); var mismatch = Line(r, 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/receipts/{id}/context", new ReceiptContextRequest(null, "Test", "2026-04-11"))).StatusCode);
        r = await Send(client, HttpMethod.Put, $"/api/receipts/{id}/context", new ReceiptContextRequest(null, "Test", "11/04/2026"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/receipts/{id}/lines/{incomplete}/validate", NewProduct("P", "A"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/receipts/{id}/lines/{mismatch}/validate", NewProduct("P", "A"))).StatusCode);
        await Send(client, HttpMethod.Put, $"/api/receipts/{id}/lines/{incomplete}", new CorrectReceiptLineRequest("Corrigé", 3, 1, "kg", null, false));
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var foreignFood = await Send(other, HttpMethod.Post, "/api/foods", new { name = "Autre", referenceUnit = "", nutrition = (object?)null });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/receipts/{id}/lines/{incomplete}/validate", NewProduct("P", foodId: Id(foreignFood)))).StatusCode);
        await using (var db = factory.CreateDbContext())
        {
            // Fail after EF has attempted catalogue inserts; the transaction must undo everything.
#pragma warning disable EF1002 // The only interpolated value is an internally generated Guid in D format.
            await db.Database.ExecuteSqlRawAsync($"""
                CREATE OR REPLACE FUNCTION receipt_test_failure() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Injected purchase failure'; END $$;
                CREATE TRIGGER receipt_test_failure BEFORE INSERT ON purchases
                FOR EACH ROW WHEN (NEW."ReceiptLineId" = '{incomplete:D}'::uuid) EXECUTE FUNCTION receipt_test_failure();
                """);
#pragma warning restore EF1002
        }
        var failed = await client.PostAsJsonAsync($"/api/receipts/{id}/lines/{incomplete}/validate", NewProduct("P", "A"));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER receipt_test_failure ON purchases; DROP FUNCTION receipt_test_failure();");
            var household = (await db.Receipts.SingleAsync(x => x.Id == id)).HouseholdId;
            Assert.Empty(await db.Products.Where(p => p.HouseholdId == household).ToListAsync());
            Assert.Empty(await db.FoodItems.Where(p => p.HouseholdId == household).ToListAsync());
            Assert.False(await db.Purchases.AnyAsync(p => p.ReceiptLineId == incomplete));
            Assert.Equal("pending", (await db.ReceiptLines.SingleAsync(l => l.Id == incomplete)).Status);
        }
        await Send(client, HttpMethod.Post, $"/api/receipts/{id}/lines/{incomplete}/validate", NewProduct("P", "A"));
        await Send(client, HttpMethod.Put, $"/api/receipts/{id}/lines/{mismatch}", new CorrectReceiptLineRequest("Écart confirmé", 10, 2, "kg", 3, true));
        var food = Assert.Single((await client.GetFromJsonAsync<List<FoodItemDto>>("/api/foods"))!);
        await Send(client, HttpMethod.Post, $"/api/receipts/{id}/lines/{mismatch}/validate", NewProduct("Autre produit", foodId: food.Id));
        Assert.Equal(2, (await client.GetFromJsonAsync<List<JsonElement>>("/api/products"))!.Count);
        Assert.Single((await client.GetFromJsonAsync<List<FoodItemDto>>("/api/foods"))!);
    }
}
