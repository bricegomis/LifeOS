using System.Text.Json;
using LifeOS.Infrastructure.Food.Files;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LifeOS.Api.IntegrationTests;

/// <summary>
/// Roadmap step 1 bis: the whole Food module is usable from a Claude conversation, through the
/// same code as the app, and another household sees none of it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AssistantFoodToolsTests(PostgresContainerFixture postgres)
{
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 1];

    private sealed class FakeImages : IRemoteImageSource
    {
        public Task<DownloadedImage> DownloadAsync(string url, CancellationToken ct) =>
            Task.FromResult(new DownloadedImage(Png, "image/png"));
    }

    [Fact]
    public async Task The_assistant_fills_foods_products_prices_recipes_and_reviews_of_its_household_only()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"lifeos-assistant-images-{Guid.NewGuid():N}");
        try
        {
            await using var factory = new LifeOSApiFactory(postgres.ConnectionString, foodImageStoragePath: storagePath);
            await using var app = factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.AddSingleton<IRemoteImageSource, FakeImages>()));
            await using var mcp = await Connect(app, Guid.NewGuid());
            await using var neighbour = await Connect(app, Guid.NewGuid());

            var tools = (await mcp.ListToolsAsync()).Select(tool => tool.Name).ToList();
            Assert.Contains("create_food", tools);
            Assert.Contains("create_recipe", tools);
            Assert.Contains("add_price", tools);
            Assert.Contains("get_receipt_photo", tools);

            var rice = await Call(mcp, "create_food", new()
            {
                ["name"] = "Riz basmati", ["referenceUnit"] = "100 g", ["calories"] = 350, ["proteins"] = 8,
                ["carbs"] = 77, ["fats"] = 1, ["fibers"] = 2, ["fodmap"] = "low", ["nova"] = 1,
            });
            var riceId = rice.GetProperty("id").GetString();
            Assert.Empty(rice.GetProperty("missingFields").EnumerateArray());
            var egg = await Call(mcp, "create_food", new() { ["name"] = "Œuf", ["referenceUnit"] = "1 pièce", ["calories"] = 70 });
            var eggId = egg.GetProperty("id").GetString();

            var withImage = await Call(mcp, "set_food_image_from_url", new() { ["foodId"] = riceId, ["url"] = "https://example.com/riz.png" });
            Assert.True(withImage.GetProperty("hasImage").GetBoolean());
            Assert.Single(Directory.GetFiles(storagePath));

            var found = await Call(mcp, "search_foods", new() { ["query"] = "BASMATI" });
            Assert.Equal("Riz basmati", Assert.Single(found.EnumerateArray()).GetProperty("name").GetString());

            var store = await Call(mcp, "create_store", new() { ["name"] = "Biocoop", ["isOrganic"] = true });
            var product = await Call(mcp, "create_product", new()
            {
                ["foodId"] = riceId, ["name"] = "Riz basmati 1 kg", ["packageQuantity"] = 1, ["packageUnit"] = "kg",
            });
            var priced = await Call(mcp, "add_price", new()
            {
                ["productId"] = product.GetProperty("id").GetString(), ["storeId"] = store.GetProperty("id").GetString(),
                ["price"] = 3.5m, ["observedOn"] = "2026-11-10",
            });
            Assert.Equal(3.5m, Assert.Single(priced.GetProperty("latestPrices").EnumerateArray()).GetProperty("unitPrice").GetDecimal());

            var recipe = await Call(mcp, "create_recipe", new()
            {
                ["name"] = "Riz aux œufs", ["servings"] = 2, ["durationMinutes"] = 20, ["tags"] = new[] { "plat" },
                ["ingredients"] = new object[]
                {
                    new { foodId = riceId, quantity = 200, unit = "g" },
                    new { foodId = eggId, quantity = 4, unit = "pièce" },
                },
            });
            var recipeId = recipe.GetProperty("id").GetString();
            Assert.Equal(["Riz basmati", "Œuf"], recipe.GetProperty("ingredients").EnumerateArray().Select(i => i.GetProperty("foodName").GetString()));
            var calories = recipe.GetProperty("nutrition").GetProperty("perServing").EnumerateArray()
                .Single(n => n.GetProperty("nutrient").GetString() == "Calories");
            Assert.Equal(490, calories.GetProperty("value").GetDouble(), 3);
            // The egg has no proteins yet: the totals are partial and name it.
            Assert.Equal(["Œuf"], recipe.GetProperty("nutrition").GetProperty("incompleteFoods").EnumerateArray().Select(f => f.GetString()));
            // 200 g of rice at 3.50 €/kg, the egg has no price: 0.70 € for two servings.
            Assert.Equal(0.35m, recipe.GetProperty("cost").GetProperty("minPerServing").GetDecimal());

            var household = await Call(mcp, "get_household", new());
            var profileId = household.GetProperty("profiles").EnumerateArray().First().GetProperty("id").GetString();
            await Call(mcp, "save_review", new() { ["profileId"] = profileId, ["recipeId"] = recipeId, ["score"] = 0 });
            var review = await Call(mcp, "save_review", new() { ["profileId"] = profileId, ["recipeId"] = recipeId, ["score"] = 8, ["comment"] = "Bon" });
            Assert.Equal(8, review.GetProperty("score").GetInt32());
            var sheet = await Call(mcp, "get_recipe", new() { ["recipeId"] = recipeId });
            Assert.Equal(8, Assert.Single(sheet.GetProperty("reviews").EnumerateArray()).GetProperty("score").GetInt32());

            Assert.Empty((await Call(mcp, "list_receipts", new())).EnumerateArray());

            // Another household sees nothing and cannot reach these ids.
            Assert.Empty((await Call(neighbour, "search_foods", new())).EnumerateArray());
            Assert.Empty((await Call(neighbour, "search_recipes", new())).EnumerateArray());
            Assert.Empty((await Call(neighbour, "list_stores", new())).EnumerateArray());
            Assert.Contains("introuvable", await Error(neighbour, "get_food", new() { ["foodId"] = riceId }));
            Assert.Contains("introuvable", await Error(neighbour, "get_recipe", new() { ["recipeId"] = recipeId }));
            Assert.Contains("aliment actif", await Error(neighbour, "create_product", new()
            {
                ["foodId"] = riceId, ["name"] = "Vol", ["packageQuantity"] = 1, ["packageUnit"] = "kg",
            }));
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task Broken_rules_reach_the_assistant_as_readable_errors_and_nothing_is_half_created()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        await using var mcp = await Connect(factory, Guid.NewGuid());

        Assert.Contains("NOVA", await Error(mcp, "create_food", new() { ["name"] = "Pain", ["nova"] = 7 }));
        Assert.Contains("https", await Error(mcp, "set_food_image_from_url", new()
        {
            ["foodId"] = (await Call(mcp, "create_food", new() { ["name"] = "Pain" })).GetProperty("id").GetString(),
            ["url"] = "http://example.com/pain.jpg",
        }));
        Assert.Contains("aliment actif", await Error(mcp, "create_recipe", new()
        {
            ["name"] = "Fantôme", ["servings"] = 1,
            ["ingredients"] = new object[] { new { foodId = Guid.NewGuid(), quantity = 1, unit = "g" } },
        }));
        Assert.Empty((await Call(mcp, "search_recipes", new() { ["includeArchived"] = true })).EnumerateArray());
        Assert.Contains("foodId", await Error(mcp, "list_reviews", new()));
    }

    private static async Task<McpClient> Connect(WebApplicationFactory<Program> factory, Guid user)
    {
        var http = factory.CreateClient().AsUser(user);
        return await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            http));
    }

    private static async Task<JsonElement> Call(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        var text = Text(result);
        Assert.True(result.IsError != true, $"{tool} failed: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> Error(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, Json(arguments));
        Assert.True(result.IsError, $"{tool} should have failed: {Text(result)}");
        return Text(result);
    }

    /// <summary>Arguments as JSON, as Claude sends them (anonymous objects included).</summary>
    private static Dictionary<string, object?> Json(Dictionary<string, object?> arguments) =>
        arguments.ToDictionary(pair => pair.Key, pair => (object?)JsonSerializer.SerializeToElement(pair.Value));

    private static string Text(CallToolResult result) =>
        string.Join("", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
