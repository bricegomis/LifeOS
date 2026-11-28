using System.ComponentModel.DataAnnotations;
using LifeOS.Domain.Reading;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Reading;

/// <param name="TotalTomes">Number of tomes of the series when known (1 to 999); it shows the missing ones.</param>
public sealed record BookSeriesRequest(
    [property: Required, StringLength(BookSeries.MaxNameLength)] string Name,
    [property: Range(1, BookSeries.MaxTomes)] int? TotalTomes);

/// <param name="MissingTomes">Whole numbers 1..TotalTomes that no book of the series holds.</param>
public sealed record BookSeriesDto(Guid Id, string Name, int? TotalTomes, int BookCount, int ReadCount, int ToReadCount,
    List<int> MissingTomes);

/// <param name="Book">The book of this tome; null for a missing tome.</param>
public sealed record BookSeriesTomeDto(decimal? Number, BookDto? Book);

/// <param name="Tomes">The series' books by tome number (books without a number last) and its missing tomes in their place.</param>
public sealed record BookSeriesDetailDto(BookSeriesDto Series, List<BookSeriesTomeDto> Tomes);

/// <summary>The household's book series (ADR 0022), shared by the endpoints and the assistant tools.</summary>
public sealed class BookSeriesLibrary(LifeOSDbContext db, TimeProvider clock)
{
    public async Task<List<BookSeriesDto>> ListAsync(Guid householdId, CancellationToken ct)
    {
        var series = await db.Series.Where(s => s.HouseholdId == householdId).ToListAsync(ct);
        var books = (await db.Books.Where(b => b.HouseholdId == householdId && b.SeriesId != null).ToListAsync(ct)).ToLookup(b => b.SeriesId!.Value);
        return series.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(s => Dto(s, books[s.Id])).ToList();
    }

    public async Task<BookSeriesDetailDto?> GetAsync(Guid householdId, Guid id, CancellationToken ct) =>
        await FindAsync(householdId, id, ct) is { } series ? await DetailAsync(series, ct) : null;

    public async Task<BookSeriesDetailDto> CreateAsync(Guid householdId, BookSeriesRequest r, CancellationToken ct)
    {
        var series = BookSeries.Create(householdId, r.Name, r.TotalTomes, clock.GetUtcNow());
        db.Series.Add(series);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(series, ct);
    }

    public async Task<BookSeriesDetailDto?> UpdateAsync(Guid householdId, Guid id, BookSeriesRequest r, CancellationToken ct)
    {
        var series = await FindAsync(householdId, id, ct);
        if (series is null) return null;
        series.Update(r.Name, r.TotalTomes, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await DetailAsync(series, ct);
    }

    /// <summary>Deletes the series; its books stay in the library, without series nor tome.</summary>
    public async Task<bool> DeleteAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var series = await FindAsync(householdId, id, ct);
        if (series is null) return false;
        var now = clock.GetUtcNow();
        foreach (var book in await db.Books.Where(b => b.HouseholdId == householdId && b.SeriesId == id).ToListAsync(ct))
            book.SetSeries(null, null, now);
        await db.SaveChangesAsync(ct);
        db.Series.Remove(series);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<BookSeriesDetailDto> DetailAsync(BookSeries series, CancellationToken ct)
    {
        var books = await db.Books.Where(b => b.HouseholdId == series.HouseholdId && b.SeriesId == series.Id).ToListAsync(ct);
        var tomes = books.Select(b => new BookSeriesTomeDto(b.Tome, BookDto.From(b, series.Name)))
            .Concat(series.MissingTomes(books.Select(b => b.Tome)).Select(n => new BookSeriesTomeDto(n, null)))
            .OrderBy(t => t.Number is null).ThenBy(t => t.Number).ThenBy(t => t.Book?.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new BookSeriesDetailDto(Dto(series, books), tomes);
    }

    private static BookSeriesDto Dto(BookSeries series, IEnumerable<Book> books)
    {
        var list = books.ToList();
        return new BookSeriesDto(series.Id, series.Name, series.TotalTomes, list.Count, list.Count(b => b.State == "read"),
            list.Count(b => b.State == "toread"), series.MissingTomes(list.Select(b => b.Tome)).ToList());
    }

    private Task<BookSeries?> FindAsync(Guid householdId, Guid id, CancellationToken ct) =>
        db.Series.SingleOrDefaultAsync(s => s.Id == id && s.HouseholdId == householdId, ct);
}
