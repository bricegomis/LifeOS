using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using LifeOS.Api.Files;
using LifeOS.Application.Reading;
using LifeOS.Domain.Reading;
using LifeOS.Infrastructure.Files;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Reading;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Reading;

/// <param name="Format">paper, ereader, audio or comic.</param>
/// <param name="SeriesId">A series of the household; none = no series (and no tome).</param>
/// <param name="Tome">The tome number in the series (decimal allowed: 2.5).</param>
public sealed record BookRequest(
    [property: Required, StringLength(Book.MaxTitleLength)] string Title,
    [property: StringLength(Book.MaxAuthorsLength)] string? Authors,
    string? Format,
    List<string>? Genres,
    [property: StringLength(Book.MaxNotesLength)] string? Notes,
    Guid? SeriesId = null,
    decimal? Tome = null);

/// <param name="State">How the book enters the library: toread (default), reading (started today), read (already read,
/// without dates) or notwanted.</param>
public sealed record BookCreateRequest(
    [property: Required, StringLength(Book.MaxTitleLength)] string Title,
    [property: StringLength(Book.MaxAuthorsLength)] string? Authors,
    string? Format,
    List<string>? Genres,
    [property: StringLength(Book.MaxNotesLength)] string? Notes,
    Guid? SeriesId = null,
    decimal? Tome = null,
    string? State = null,
    Guid? MemberProfileId = null);

public sealed record BookStartRequest(DateOnly? Day);

public sealed record BookFinishRequest(DateOnly? Day, [property: Range(0, 10)] int? Rating,
    [property: StringLength(BookReading.MaxReviewLength)] string? Review);

public sealed record BookStopRequest(DateOnly? Day, [property: StringLength(Book.MaxReasonLength)] string? Reason);

/// <param name="Outcome">read or stopped (a reading already over).</param>
public sealed record BookPastReadingRequest(
    [property: Required] string Outcome,
    DateOnly? StartedOn,
    DateOnly? FinishedOn,
    [property: Range(0, 10)] int? Rating,
    [property: StringLength(BookReading.MaxReviewLength)] string? Review,
    [property: StringLength(Book.MaxReasonLength)] string? Reason);

public sealed record BookReadingUpdateRequest(
    DateOnly? StartedOn,
    DateOnly? FinishedOn,
    [property: Range(0, 10)] int? Rating,
    [property: StringLength(BookReading.MaxReviewLength)] string? Review,
    [property: StringLength(Book.MaxReasonLength)] string? Reason);

public sealed record BookNotWantedRequest([property: StringLength(Book.MaxReasonLength)] string? Reason);

public sealed record BookQuoteRequest(
    [property: Required, StringLength(BookQuote.MaxTextLength)] string Text,
    [property: StringLength(BookQuote.MaxPlaceLength)] string? Place);

public sealed record BookReadingDto(Guid Id, string Outcome, DateOnly? StartedOn, DateOnly? FinishedOn, int? Rating, string? Review,
    string? StopReason);

public sealed record BookQuoteDto(Guid Id, string Text, string? Place);

/// <param name="State">toread, reading, read, stopped or notwanted, computed from the readings.</param>
/// <param name="LastRating">The rating of the latest reading that has one.</param>
/// <param name="ReadCount">Readings finished (read).</param>
/// <param name="Readings">Oldest first.</param>
public sealed record BookDto(
    Guid Id,
    Guid MemberProfileId,
    string Title,
    string Authors,
    string Format,
    List<string> Genres,
    Guid? SeriesId,
    string? SeriesName,
    decimal? Tome,
    string? Notes,
    string State,
    string? NotWantedReason,
    int? LastRating,
    int ReadCount,
    DateOnly? StartedOn,
    DateOnly? LastFinishedOn,
    bool HasImage,
    List<BookReadingDto> Readings,
    List<BookQuoteDto> Quotes,
    DateTimeOffset UpdatedAt)
{
    public static BookDto From(Book b, string? seriesName) => new(
        b.Id, b.MemberProfileId, b.Title, b.Authors, b.Format, b.Genres.ToList(), b.SeriesId, seriesName, b.Tome, b.Notes, b.State,
        b.NotWantedReason, b.LastRating, b.Readings.Count(r => r.Outcome == "read"), b.CurrentReading?.StartedOn,
        b.Readings.Where(r => r.Outcome == "read").Max(r => r.FinishedOn), b.HasImage,
        b.OrderedReadings.Select(r => new BookReadingDto(r.Id, r.Outcome, r.StartedOn, r.FinishedOn, r.Rating, r.Review, r.StopReason)).ToList(),
        b.OrderedQuotes.Select(q => new BookQuoteDto(q.Id, q.Text, q.Place)).ToList(),
        b.UpdatedAt);
}

/// <summary>
/// The household's books (ADR 0022), shared by the endpoints and the assistant tools; rules in <see cref="Book"/>.
/// A null result means "not in this household".
/// </summary>
public sealed class BookLibrary(LifeOSDbContext db, BookImageStorage images, IRemoteImageSource remoteImages, ReadingProfiles profiles,
    TimeProvider clock, ILogger<BookLibrary> logger)
{
    /// <summary>
    /// Books filtered by state (all = every state but notwanted; any = every book), text (title, authors, series, genres,
    /// ignoring case and accents, every word found), format, genre and series; in progress first, then by title.
    /// </summary>
    public async Task<List<BookDto>> SearchAsync(Guid householdId, string? state, string? query, string? format, string? genre,
        Guid? seriesId, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(state) ? "all" : state.Trim();
        if (code is not ("all" or "any") && !Book.States.ContainsKey(code))
            throw new ArgumentException("État : all, any, toread, reading, read, stopped ou notwanted.");
        var books = await db.Books.Where(b => b.HouseholdId == householdId
            && (format == null || b.Format == format) && (seriesId == null || b.SeriesId == seriesId)).ToListAsync(ct);
        var series = await SeriesNamesAsync(householdId, ct);
        var words = Normalize(query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Ordered(books
                .Where(b => code switch { "any" => true, "all" => b.State != "notwanted", _ => b.State == code })
                .Where(b => string.IsNullOrWhiteSpace(genre) || b.Genres.Contains(genre.Trim(), StringComparer.OrdinalIgnoreCase))
                .Where(b =>
                {
                    if (words.Length == 0) return true;
                    var text = Normalize(string.Join(' ', [b.Title, b.Authors, SeriesName(series, b) ?? "", .. b.Genres]));
                    return words.All(text.Contains);
                }))
            .Select(b => BookDto.From(b, SeriesName(series, b))).ToList();
    }

    public async Task<BookDto?> GetAsync(Guid householdId, Guid id, CancellationToken ct) =>
        await FindAsync(householdId, id, ct) is { } book ? await DtoAsync(book, ct) : null;

    /// <summary>The genres already used by the household's books, in alphabetical order.</summary>
    public async Task<List<string>> GenresAsync(Guid householdId, CancellationToken ct) =>
        (await db.Books.Where(b => b.HouseholdId == householdId).Select(b => b.Genres).ToListAsync(ct))
            .SelectMany(g => g).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();

    public async Task<BookDto> CreateAsync(Guid householdId, BookCreateRequest r, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var profileId = await profiles.ResolveAsync(householdId, r.MemberProfileId, ct);
        var book = Book.Create(householdId, profileId, r.Title, r.Authors, r.Format ?? "paper", r.Genres, r.Notes, now);
        book.SetSeries(await SeriesAsync(householdId, r.SeriesId, ct), r.Tome, now);
        var today = ReadingClock.Today(clock);
        switch (string.IsNullOrWhiteSpace(r.State) ? "toread" : r.State.Trim())
        {
            case "toread": break;
            case "reading": book.Start(null, today, now); break;
            case "read": book.AddPastReading("read", null, null, null, null, null, today, now); break;
            case "notwanted": book.MarkNotWanted(null, now); break;
            default: throw new ArgumentException("État de départ : toread, reading, read ou notwanted.");
        }
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);
        return await DtoAsync(book, ct);
    }

    public Task<BookDto?> UpdateAsync(Guid householdId, Guid id, BookRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, async book =>
        {
            var now = clock.GetUtcNow();
            book.Update(r.Title, r.Authors, r.Format ?? book.Format, r.Genres, r.Notes, now);
            book.SetSeries(await SeriesAsync(householdId, r.SeriesId, ct), r.Tome, now);
        }, ct);

    public async Task<bool> DeleteAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var book = await FindAsync(householdId, id, ct);
        if (book is null) return false;
        var image = book.ImagePath;
        db.Books.Remove(book);
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(image, e => logger.LogError(e, "Unable to remove deleted book cover"));
        return true;
    }

    public Task<BookDto?> StartAsync(Guid householdId, Guid id, DateOnly? day, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.Start(day, ReadingClock.Today(clock), clock.GetUtcNow()), ct);

    public Task<BookDto?> FinishAsync(Guid householdId, Guid id, BookFinishRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.Finish(r.Day, r.Rating, r.Review, ReadingClock.Today(clock), clock.GetUtcNow()), ct);

    public Task<BookDto?> StopAsync(Guid householdId, Guid id, BookStopRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.Stop(r.Day, r.Reason, ReadingClock.Today(clock), clock.GetUtcNow()), ct);

    public Task<BookDto?> AddPastReadingAsync(Guid householdId, Guid id, BookPastReadingRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.AddPastReading(r.Outcome.Trim(), r.StartedOn, r.FinishedOn, r.Rating, r.Review, r.Reason,
            ReadingClock.Today(clock), clock.GetUtcNow()), ct);

    public Task<BookDto?> UpdateReadingAsync(Guid householdId, Guid id, Guid readingId, BookReadingUpdateRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.UpdateReading(readingId, r.StartedOn, r.FinishedOn, r.Rating, r.Review, r.Reason,
            ReadingClock.Today(clock), clock.GetUtcNow()), ct);

    public Task<BookDto?> RemoveReadingAsync(Guid householdId, Guid id, Guid readingId, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.RemoveReading(readingId, clock.GetUtcNow()), ct);

    public Task<BookDto?> MarkNotWantedAsync(Guid householdId, Guid id, string? reason, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.MarkNotWanted(reason, clock.GetUtcNow()), ct);

    public Task<BookDto?> ClearNotWantedAsync(Guid householdId, Guid id, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.ClearNotWanted(clock.GetUtcNow()), ct);

    public Task<BookDto?> AddQuoteAsync(Guid householdId, Guid id, BookQuoteRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.AddQuote(r.Text, r.Place, clock.GetUtcNow()), ct);

    public Task<BookDto?> UpdateQuoteAsync(Guid householdId, Guid id, Guid quoteId, BookQuoteRequest r, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.UpdateQuote(quoteId, r.Text, r.Place, clock.GetUtcNow()), ct);

    public Task<BookDto?> RemoveQuoteAsync(Guid householdId, Guid id, Guid quoteId, CancellationToken ct) =>
        ChangeAsync(householdId, id, b => b.RemoveQuote(quoteId, clock.GetUtcNow()), ct);

    public async Task<(string? Path, string? ContentType)> ImageAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var image = await db.Books.Where(b => b.Id == id && b.HouseholdId == householdId)
            .Select(b => new { b.ImagePath, b.ImageContentType }).SingleOrDefaultAsync(ct);
        return (image?.ImagePath, image?.ImageContentType);
    }

    public async Task<BookDto?> ReplaceImageAsync(Guid householdId, Guid id, Stream content, long length, string? contentType, CancellationToken ct)
    {
        var book = await FindAsync(householdId, id, ct);
        if (book is null) return null;
        await images.ReplaceImageAsync(content, length, contentType, book.ImagePath,
            stored => book.SetImage(stored.FileName, stored.ContentType), () => db.SaveChangesAsync(ct), logger, ct);
        return await DtoAsync(book, ct);
    }

    /// <summary>Downloads a cover found by the assistant, then stores it like an upload.</summary>
    public async Task<BookDto?> ReplaceImageFromUrlAsync(Guid householdId, Guid id, string url, CancellationToken ct)
    {
        if (await FindAsync(householdId, id, ct) is null) return null;
        var image = await remoteImages.DownloadAsync(url, ct);
        using var content = new MemoryStream(image.Bytes);
        return await ReplaceImageAsync(householdId, id, content, image.Bytes.Length, image.ContentType, ct);
    }

    public async Task<BookDto?> RemoveImageAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var book = await FindAsync(householdId, id, ct);
        if (book is null) return null;
        var oldPath = book.ImagePath;
        book.RemoveImage();
        await db.SaveChangesAsync(ct);
        images.DeleteQuietly(oldPath, e => logger.LogError(e, "Unable to remove book cover"));
        return await DtoAsync(book, ct);
    }

    /// <summary>In progress first, then to read, read, stopped and not wanted; by title within a state.</summary>
    public static IEnumerable<Book> Ordered(IEnumerable<Book> books) => books
        .OrderBy(b => b.State switch { "reading" => 0, "toread" => 1, "read" => 2, "stopped" => 3, _ => 4 })
        .ThenBy(b => b.Title, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Lower case, without accents, apostrophes and punctuation as spaces.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return builder.ToString();
    }

    internal async Task<BookDto> DtoAsync(Book book, CancellationToken ct)
    {
        var name = book.SeriesId is { } id
            ? await db.Series.Where(s => s.Id == id && s.HouseholdId == book.HouseholdId).Select(s => s.Name).SingleOrDefaultAsync(ct)
            : null;
        return BookDto.From(book, name);
    }

    internal async Task<Dictionary<Guid, string>> SeriesNamesAsync(Guid householdId, CancellationToken ct) =>
        await db.Series.Where(s => s.HouseholdId == householdId).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

    private static string? SeriesName(Dictionary<Guid, string> names, Book book) =>
        book.SeriesId is { } id ? names.GetValueOrDefault(id) : null;

    private async Task<BookDto?> ChangeAsync(Guid householdId, Guid id, Action<Book> change, CancellationToken ct) =>
        await ChangeAsync(householdId, id, b => { change(b); return Task.CompletedTask; }, ct);

    private async Task<BookDto?> ChangeAsync(Guid householdId, Guid id, Func<Book, Task> change, CancellationToken ct)
    {
        var book = await FindAsync(householdId, id, ct);
        if (book is null) return null;
        await change(book);
        await db.SaveChangesAsync(ct);
        return await DtoAsync(book, ct);
    }

    private async Task<Guid?> SeriesAsync(Guid householdId, Guid? seriesId, CancellationToken ct)
    {
        if (seriesId is not { } id || id == Guid.Empty) return null;
        return await db.Series.AnyAsync(s => s.Id == id && s.HouseholdId == householdId, ct)
            ? id
            : throw new ArgumentException("Série introuvable dans votre foyer.");
    }

    private Task<Book?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.Books.SingleOrDefaultAsync(b => b.Id == id && b.HouseholdId == householdId, ct);
}
