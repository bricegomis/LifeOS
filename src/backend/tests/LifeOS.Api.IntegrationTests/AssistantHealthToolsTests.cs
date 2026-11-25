using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Health;
using LifeOS.Application.Health.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LifeOS.Api.IntegrationTests;

/// <summary>Roadmap step 5: the health module is usable from a Claude conversation, for the connected account only.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AssistantHealthToolsTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task The_assistant_ticks_notes_reads_documents_and_fills_appointments_of_its_account_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        var user = Guid.NewGuid();
        await using var mcp = await Connect(factory, user);
        await using var stranger = await Connect(factory, Guid.NewGuid());

        var supplement = await Call(mcp, "create_supplement", new()
        {
            ["name"] = "Vitamine D3", ["dose"] = 1, ["unit"] = "goutte", ["packageContent"] = 300, ["packagePrice"] = 12, ["moment"] = "matin",
            ["links"] = new[] { "https://example.com/d3" },
        });
        var supplementId = supplement.GetProperty("id").GetString();
        Assert.True((await Call(mcp, "set_supplement_taken", new() { ["supplementId"] = supplementId })).GetProperty("takenToday").GetBoolean());
        Assert.Equal("Vitamine D3 forte", (await Call(mcp, "update_supplement", new() { ["supplementId"] = supplementId, ["name"] = "Vitamine D3 forte" }))
            .GetProperty("name").GetString());

        var journal = await Call(mcp, "get_symptom_journal", new());
        var typeId = journal.GetProperty("types")[0].GetProperty("id").GetString();
        var entry = await Call(mcp, "log_symptom", new() { ["symptomTypeId"] = typeId, ["intensity"] = 6, ["note"] = "Après le repas" });
        Assert.Equal(6, entry.GetProperty("intensity").GetInt32());
        Assert.Contains("between 1 and 10", await Error(mcp, "log_symptom", new() { ["symptomTypeId"] = typeId, ["intensity"] = 12 }));

        // A document photographed in the app is read by the assistant, which then enters the values.
        using var http = factory.CreateClient().AsUser(user);
        var document = await (await http.PostAsJsonAsync("/api/health/documents", new HealthDocumentRequest("resultat", "Bilan fer", null, "Cerballiance", null)))
            .Content.ReadFromJsonAsync<HealthDocumentDto>();
        var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent("%PDF-1.4\n%%EOF"u8.ToArray());
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "bilan.pdf");
        var fileId = (await (await http.PostAsync($"/api/health/documents/{document!.Id}/files", form)).Content.ReadFromJsonAsync<HealthDocumentDto>())!.Files[0].Id;
        var read = await mcp.CallToolAsync("get_health_document_file", Json(new() { ["documentId"] = document.Id, ["fileId"] = fileId }));
        Assert.True(read.IsError != true, Text(read));
        var blob = Assert.IsType<BlobResourceContents>(Assert.Single(read.Content.OfType<EmbeddedResourceBlock>()).Resource);
        Assert.Equal("application/pdf", blob.MimeType);

        var marker = await Call(mcp, "create_marker", new() { ["name"] = "Ferritine", ["unit"] = "µg/L", ["labMin"] = 30, ["labMax"] = 400, ["targetMin"] = 80 });
        var analysis = await Call(mcp, "create_analysis", new()
        {
            ["documentId"] = document.Id, ["laboratory"] = "Cerballiance",
            ["values"] = new[] { new { markerId = marker.GetProperty("id").GetString(), value = 62 } },
        });
        Assert.Equal("normal", analysis.GetProperty("values")[0].GetProperty("status").GetString());
        var documents = await Call(mcp, "list_health_documents", new() { ["type"] = "resultat" });
        Assert.Equal(analysis.GetProperty("id").GetString(), documents[0].GetProperty("analysisIds")[0].GetString());

        // From a booking confirmation e-mail, then moved.
        var appointment = await Call(mcp, "create_appointment", new()
        {
            ["day"] = "2030-03-04", ["time"] = "09:30", ["who"] = "Dr Martin, dermatologue", ["bookingUrl"] = "https://www.doctolib.fr/dermatologue/paris/dr-martin",
        });
        var moved = await Call(mcp, "update_appointment", new() { ["appointmentId"] = appointment.GetProperty("id").GetString(), ["day"] = "2030-03-11" });
        Assert.Equal(("2030-03-11", "Dr Martin, dermatologue"), (moved.GetProperty("day").GetString(), moved.GetProperty("who").GetString()));
        Assert.Single((await Call(mcp, "list_appointments", new())).GetProperty("upcoming").EnumerateArray());

        Assert.Empty((await Call(stranger, "list_supplements", new() { ["includeStopped"] = true })).GetProperty("supplements").EnumerateArray());
        Assert.Empty((await Call(stranger, "list_health_documents", new())).EnumerateArray());
        Assert.Empty((await Call(stranger, "list_appointments", new())).GetProperty("upcoming").EnumerateArray());
        await Error(stranger, "get_health_document_file", new() { ["documentId"] = document.Id, ["fileId"] = fileId });
        await Error(stranger, "set_supplement_taken", new() { ["supplementId"] = supplementId });
    }

    private static async Task<McpClient> Connect(WebApplicationFactory<Program> factory, Guid user)
    {
        return await factory.CreateClient().AsUser(user).ConnectMcpAsync();
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

    private static Dictionary<string, object?> Json(Dictionary<string, object?> arguments) =>
        arguments.ToDictionary(pair => pair.Key, pair => (object?)JsonSerializer.SerializeToElement(pair.Value));

    private static string Text(CallToolResult result) =>
        string.Join("", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
