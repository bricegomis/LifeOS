using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LifeOS.Api.Reading;
using LifeOS.Application.Reading.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LifeOS.Api.IntegrationTests;

/// <summary>ADR 0022: the reading library (books, readings, quotes, series) per household, its tools and its read contract.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ReadingTests(PostgresContainerFixture postgres)
{
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<HttpResponseMessage> Create(HttpClient client, string title, string? state = null, Guid? series = null, decimal? tome = null,
        string format = "paper", List<string>? genres = null) =>
        client.PostAsJsonAsync("/api/reading/books", new BookCreateRequest(title, "Frank Herbert", format, genres, null, series, tome, state));

    [Fact]
    public async Task A_book_goes_from_to_read_to_read_then_is_re_read_and_the_library_filters_by_state_and_text()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var created = await Create(client, "Dune", genres: ["SF"]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dune = await Read<BookDto>(created);
        Assert.Equal("toread", dune.State);

        dune = await Read<BookDto>(await client.PostAsJsonAsync($"/api/reading/books/{dune.Id}/start", new BookStartRequest(null)));
        Assert.Equal("reading", dune.State);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/reading/books/{dune.Id}/start", new BookStartRequest(null))).StatusCode);
        dune = await Read<BookDto>(await client.PostAsJsonAsync($"/api/reading/books/{dune.Id}/finish", new BookFinishRequest(null, 9, "Génial")));
        Assert.Equal(("read", 9, 1), (dune.State, dune.LastRating!.Value, dune.ReadCount));
        dune = await Read<BookDto>(await client.PostAsJsonAsync($"/api/reading/books/{dune.Id}/start", new BookStartRequest(null)));
        dune = await Read<BookDto>(await client.PostAsJsonAsync($"/api/reading/books/{dune.Id}/finish", new BookFinishRequest(null, 10, null)));
        Assert.Equal((2, 10), (dune.ReadCount, dune.LastRating!.Value));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/reading/books/{dune.Id}/finish",
            new BookFinishRequest(null, 11, null))).StatusCode);

        var sapiens = await Read<BookDto>(await client.PostAsJsonAsync("/api/reading/books",
            new BookCreateRequest("Sapiens", "Yuval Noah Harari", "ereader", ["Essai"], null, State: "reading")));
        sapiens = await Read<BookDto>(await client.PostAsJsonAsync($"/api/reading/books/{sapiens.Id}/stop", new BookStopRequest(null, "Trop long")));
        Assert.Equal(("stopped", "Trop long"), (sapiens.State, sapiens.Readings.Single().StopReason));
        var davinci = await Read<BookDto>(await Create(client, "Da Vinci Code", "notwanted"));
        Assert.Equal("notwanted", davinci.State);
        var etranger = await Read<BookDto>(await Create(client, "L'Étranger", "read"));
        Assert.Equal(("read", (DateOnly?)null), (etranger.State, etranger.Readings.Single().FinishedOn));

        var readingId = etranger.Readings.Single().Id;
        etranger = await Read<BookDto>(await client.PutAsJsonAsync($"/api/reading/books/{etranger.Id}/readings/{readingId}",
            new BookReadingUpdateRequest(null, new DateOnly(2008, 6, 1), 8, "Lu au lycée", null)));
        Assert.Equal(8, etranger.LastRating);
        etranger = await Read<BookDto>(await client.PostAsJsonAsync($"/api/reading/books/{etranger.Id}/quotes",
            new BookQuoteRequest("Aujourd'hui, maman est morte.", "Incipit")));
        Assert.Equal("Incipit", etranger.Quotes.Single().Place);

        async Task<List<string>> Titles(string query) =>
            (await client.GetFromJsonAsync<List<BookDto>>($"/api/reading/books{query}"))!.Select(b => b.Title).ToList();
        Assert.Equal(["Dune", "L'Étranger", "Sapiens"], await Titles(""));
        Assert.Equal(["Dune", "L'Étranger", "Sapiens", "Da Vinci Code"], await Titles("?state=any"));
        Assert.Equal(["Da Vinci Code"], await Titles("?state=notwanted"));
        Assert.Equal(["L'Étranger"], await Titles("?q=ETRANGER"));
        Assert.Equal(["Sapiens"], await Titles("?format=ereader"));
        Assert.Equal(["Dune"], await Titles("?genre=sf"));
        Assert.Equal(["Essai", "SF"], (await client.GetFromJsonAsync<List<string>>("/api/reading/books/genres"))!);

        davinci = await Read<BookDto>(await client.DeleteAsync($"/api/reading/books/{davinci.Id}/not-wanted"));
        Assert.Equal("toread", davinci.State);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/reading/books/{davinci.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/reading/books/{davinci.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_series_lists_its_tomes_in_order_with_the_missing_ones_and_deleting_it_keeps_its_books()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        var created = await client.PostAsJsonAsync("/api/reading/series", new BookSeriesRequest("Dune", 6));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var series = (await Read<BookSeriesDetailDto>(created)).Series;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/reading/series", new BookSeriesRequest("Dune", null))).StatusCode);

        await Read<BookDto>(await Create(client, "Le Messie de Dune", "read", series.Id, 2));
        var first = await Read<BookDto>(await Create(client, "Dune", "read", series.Id, 1));
        Assert.Equal(("Dune", 1m), (first.SeriesName, first.Tome!.Value));
        await Read<BookDto>(await Create(client, "Les Enfants de Dune", null, series.Id, 3));
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, "Sans série", null, null, 4)).StatusCode);

        var detail = await Read<BookSeriesDetailDto>(await client.GetAsync($"/api/reading/series/{series.Id}"));
        Assert.Equal([1m, 2m, 3m, 4m, 5m, 6m], detail.Tomes.Select(t => t.Number!.Value));
        Assert.Equal([4, 5, 6], detail.Series.MissingTomes);
        Assert.Equal((3, 2, 1), (detail.Series.BookCount, detail.Series.ReadCount, detail.Series.ToReadCount));
        Assert.Equal(3, (await client.GetFromJsonAsync<List<BookDto>>($"/api/reading/books?seriesId={series.Id}&q=dune"))!.Count);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/reading/series/{series.Id}")).StatusCode);
        first = (await client.GetFromJsonAsync<BookDto>($"/api/reading/books/{first.Id}"))!;
        Assert.Equal(((Guid?)null, (decimal?)null), (first.SeriesId, first.Tome));
        Assert.Empty((await client.GetFromJsonAsync<List<BookSeriesDto>>("/api/reading/series"))!);
    }

    [Fact]
    public async Task Books_series_and_covers_stay_in_their_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient().AsUser(Guid.NewGuid());
        using var other = factory.CreateClient().AsUser(Guid.NewGuid());
        var series = (await Read<BookSeriesDetailDto>(await client.PostAsJsonAsync("/api/reading/series", new BookSeriesRequest("Blacksad", 7)))).Series;
        var book = await Read<BookDto>(await Create(client, "Arctic-Nation", null, series.Id, 2, "comic"));

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "couverture.png");
        Assert.True((await Read<BookDto>(await client.PutAsync($"/api/reading/books/{book.Id}/image", form))).HasImage);
        using (var read = await client.GetAsync($"/api/reading/books/{book.Id}/image"))
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reading/books/{book.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reading/books/{book.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/reading/books/{book.Id}/start", new BookStartRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/reading/books/{book.Id}",
            new BookRequest("Piratage", null, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/reading/books/{book.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reading/series/{series.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/reading/series/{series.Id}")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<BookDto>>("/api/reading/books?state=any"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<BookSeriesDto>>("/api/reading/series"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<string>>("/api/reading/books/genres"))!);

        // A series or a profile of another household is rejected.
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(other, "Ailleurs", null, series.Id, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/reading/books",
            new BookCreateRequest("Ailleurs", null, null, null, null, MemberProfileId: book.MemberProfileId))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/reading/books/{book.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/reading/books/{book.Id}/image")).StatusCode);
    }

    [Fact]
    public async Task The_assistant_adds_reads_and_recommends_from_its_household_only()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        await using var mcp = await factory.CreateClient().AsUser(Guid.NewGuid()).ConnectMcpAsync();
        await using var neighbour = await factory.CreateClient().AsUser(Guid.NewGuid()).ConnectMcpAsync();

        var tools = (await mcp.ListToolsAsync()).Select(tool => tool.Name).ToList();
        Assert.Contains("create_book", tools);
        Assert.Contains("set_book_cover_from_url", tools);

        var series = (await Call(mcp, "create_book_series", new() { ["name"] = "Dune", ["totalTomes"] = 6 })).GetProperty("series");
        var seriesId = series.GetProperty("id").GetString();
        var dune = await Call(mcp, "create_book", new()
        {
            ["title"] = "Dune", ["authors"] = "Frank Herbert", ["genres"] = new[] { "SF" }, ["state"] = "read",
            ["finishedOn"] = "2020-01-01", ["rating"] = 9, ["review"] = "Culte", ["seriesId"] = seriesId, ["tome"] = 1,
        });
        var duneId = dune.GetProperty("id").GetString();
        Assert.Equal(("read", 9), (dune.GetProperty("state").GetString(), dune.GetProperty("lastRating").GetInt32()));
        Assert.Equal("2020-01-01", dune.GetProperty("readings")[0].GetProperty("finishedOn").GetString());

        var messiah = await Call(mcp, "create_book", new() { ["title"] = "Le Messie de Dune", ["seriesId"] = seriesId, ["tome"] = 2 });
        var messiahId = messiah.GetProperty("id").GetString();
        Assert.Equal("reading", (await Call(mcp, "start_book", new() { ["bookId"] = messiahId })).GetProperty("state").GetString());
        var finished = await Call(mcp, "finish_book", new() { ["bookId"] = messiahId, ["rating"] = 0 });
        Assert.Equal(0, finished.GetProperty("lastRating").GetInt32());

        var grey = await Call(mcp, "create_book", new() { ["title"] = "Da Vinci Code" });
        var greyId = grey.GetProperty("id").GetString();
        Assert.Equal("notwanted", (await Call(mcp, "set_book_not_wanted", new() { ["bookId"] = greyId, ["reason"] = "Pas mon genre" }))
            .GetProperty("state").GetString());
        Assert.Equal(2, (await Call(mcp, "search_books", new() { ["query"] = "dune" })).GetArrayLength());
        Assert.Single((await Call(mcp, "search_books", new() { ["state"] = "notwanted" })).EnumerateArray());
        Assert.Equal([3, 4, 5, 6], (await Call(mcp, "get_book_series", new() { ["seriesId"] = seriesId }))
            .GetProperty("series").GetProperty("missingTomes").EnumerateArray().Select(n => n.GetInt32()));
        var quoted = await Call(mcp, "add_book_quote", new() { ["bookId"] = duneId, ["text"] = "La peur tue l'esprit." });
        Assert.Single(quoted.GetProperty("quotes").EnumerateArray());
        Assert.Contains("10", await Error(mcp, "finish_book", new() { ["bookId"] = duneId, ["rating"] = 12 }));

        Assert.Contains("introuvable", await Error(neighbour, "get_book", new() { ["bookId"] = duneId }));
        Assert.Contains("introuvable", await Error(neighbour, "get_book_series", new() { ["seriesId"] = seriesId }));
        Assert.Contains("introuvable", await Error(neighbour, "create_book", new() { ["title"] = "Ailleurs", ["seriesId"] = seriesId }));
        Assert.Empty((await Call(neighbour, "search_books", new() { ["state"] = "any" })).EnumerateArray());
    }

    [Fact]
    public async Task The_read_contract_reads_books_in_progress_and_ended_readings_of_one_household()
    {
        await using var factory = new LifeOSApiFactory(postgres.ConnectionString);
        using var clientA = factory.CreateClient().AsUser(Guid.NewGuid());
        using var clientB = factory.CreateClient().AsUser(Guid.NewGuid());
        var reading = await Read<BookDto>(await Create(clientA, "Born to Run", "reading", format: "audio"));
        var done = await Read<BookDto>(await Create(clientA, "Dune", "reading"));
        await Read<BookDto>(await clientA.PostAsJsonAsync($"/api/reading/books/{done.Id}/finish", new BookFinishRequest(null, 8, null)));
        await Read<BookDto>(await Create(clientB, "Autre foyer", "reading"));

        Guid household;
        await using (var db = factory.CreateDbContext())
            household = await db.Books.Where(b => b.Id == reading.Id).Select(b => b.HouseholdId).SingleAsync();
        using var scope = factory.Services.CreateScope();
        var contract = scope.ServiceProvider.GetRequiredService<ReadingReadContract>();
        var today = contract.Today();

        var current = Assert.Single(await contract.InProgressAsync(household, CancellationToken.None));
        Assert.Equal((reading.Id, "audio", today), (current.BookId, current.Format, current.StartedOn));
        var ended = Assert.Single(await contract.EndedAsync(household, today, today, CancellationToken.None));
        Assert.Equal((done.Id, "read", 8), (ended.BookId, ended.Outcome, ended.Rating!.Value));
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
