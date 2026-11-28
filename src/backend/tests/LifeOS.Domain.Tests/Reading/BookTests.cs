using LifeOS.Domain.Reading;

namespace LifeOS.Domain.Tests.Reading;

public sealed class BookTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 28);

    private static Book NewBook() => Book.Create(Guid.NewGuid(), Guid.NewGuid(), " Dune ", "Frank Herbert", "paper", ["SF", "sf", " "], null, Now);

    [Fact]
    public void A_new_book_is_to_read_and_its_genres_are_cleaned()
    {
        var book = NewBook();
        Assert.Equal(("Dune", "toread", (int?)null), (book.Title, book.State, book.LastRating));
        Assert.Equal(["SF"], book.Genres);
        Assert.Throws<ArgumentException>(() => Book.Create(Guid.NewGuid(), Guid.NewGuid(), " ", null, "paper", null, null, Now));
        Assert.Throws<ArgumentException>(() => Book.Create(Guid.NewGuid(), Guid.NewGuid(), "Dune", null, "scroll", null, null, Now));
    }

    [Fact]
    public void The_state_follows_the_last_reading_and_a_re_read_keeps_the_history()
    {
        var book = NewBook();
        book.Start(new DateOnly(2026, 10, 1), Today, Now);
        Assert.Equal("reading", book.State);
        Assert.Throws<ArgumentException>(() => book.Start(null, Today, Now));
        book.Finish(new DateOnly(2026, 10, 20), 0, " Bof. ", Today, Now);
        Assert.Equal(("read", (int?)0), (book.State, book.LastRating));

        Assert.Throws<ArgumentException>(() => book.Start(new DateOnly(2026, 10, 10), Today, Now));
        book.Start(null, Today, Now);
        book.Stop(null, "Pas le moment.", Today, Now);
        Assert.Equal(("stopped", (int?)0), (book.State, book.LastRating));
        Assert.Equal("Pas le moment.", book.LastReading!.StopReason);
        Assert.Equal(2, book.Readings.Count);

        book.RemoveReading(book.LastReading.Id, Now);
        Assert.Equal("read", book.State);
    }

    [Fact]
    public void A_finish_is_checked_and_a_failed_finish_leaves_the_reading_in_progress()
    {
        var book = NewBook();
        Assert.Throws<ArgumentException>(() => book.Finish(null, 8, null, Today, Now));
        book.Start(new DateOnly(2026, 11, 20), Today, Now);
        Assert.Throws<ArgumentException>(() => book.Finish(new DateOnly(2026, 11, 10), 8, null, Today, Now));
        Assert.Throws<ArgumentException>(() => book.Finish(null, 11, null, Today, Now));
        Assert.Throws<ArgumentException>(() => book.Finish(Today.AddDays(1), 8, null, Today, Now));
        Assert.Equal("reading", book.State);
        Assert.Throws<ArgumentException>(() => book.Start(Today.AddDays(1), Today, Now));
    }

    [Fact]
    public void A_past_reading_may_have_no_dates_and_only_a_finished_one_is_rated()
    {
        var book = NewBook();
        book.AddPastReading("read", null, null, 9, "Génial", null, Today, Now);
        Assert.Equal(("read", (int?)9), (book.State, book.LastRating));
        Assert.Throws<ArgumentException>(() => book.AddPastReading("stopped", null, null, 5, null, null, Today, Now));
        Assert.Throws<ArgumentException>(() => book.AddPastReading("reading", null, null, null, null, null, Today, Now));
        book.Start(null, Today, Now);
        Assert.Throws<ArgumentException>(() => book.AddPastReading("read", null, null, null, null, null, Today, Now));

        var first = book.OrderedReadings.First();
        book.UpdateReading(first.Id, new DateOnly(2020, 1, 1), new DateOnly(2020, 2, 1), 10, null, null, Today, Now);
        Assert.Equal((10, (string?)null), (first.Rating!.Value, first.Review));
        Assert.Throws<ArgumentException>(() => book.UpdateReading(book.CurrentReading!.Id, Today, null, 5, null, null, Today, Now));
    }

    [Fact]
    public void Not_wanted_is_only_for_a_book_never_read_and_starting_lifts_it()
    {
        var book = NewBook();
        book.MarkNotWanted(" Pas mon genre ", Now);
        Assert.Equal(("notwanted", "Pas mon genre"), (book.State, book.NotWantedReason));
        book.Start(null, Today, Now);
        Assert.Equal(("reading", false), (book.State, book.NotWanted));
        Assert.Throws<ArgumentException>(() => book.MarkNotWanted(null, Now));
    }

    [Fact]
    public void A_tome_needs_a_series_and_quotes_keep_their_order()
    {
        var book = NewBook();
        Assert.Throws<ArgumentException>(() => book.SetSeries(null, 2, Now));
        Assert.Throws<ArgumentException>(() => book.SetSeries(Guid.NewGuid(), 0, Now));
        Assert.Throws<ArgumentException>(() => book.SetSeries(Guid.NewGuid(), 1.255m, Now));
        book.SetSeries(Guid.NewGuid(), 2.5m, Now);
        Assert.Equal(2.5m, book.Tome);
        book.SetSeries(null, null, Now);
        Assert.Null(book.Tome);

        var fear = book.AddQuote(" La peur tue l'esprit. ", " Litanie ", Now);
        book.AddQuote("Le sommeil de la raison", null, Now);
        Assert.Equal(["La peur tue l'esprit.", "Le sommeil de la raison"], book.OrderedQuotes.Select(q => q.Text));
        book.RemoveQuote(fear.Id, Now);
        Assert.Single(book.Quotes);
        Assert.Throws<ArgumentException>(() => book.AddQuote(" ", null, Now));
    }

    [Fact]
    public void A_series_lists_its_missing_whole_tomes_when_its_total_is_known()
    {
        var series = BookSeries.Create(Guid.NewGuid(), "Dune", 6, Now);
        Assert.Equal([3, 5, 6], series.MissingTomes([1m, 2m, 2.5m, 4m, null]));
        series.Update("Dune", null, Now);
        Assert.Empty(series.MissingTomes([1m]));
        Assert.Throws<ArgumentException>(() => series.Update("Dune", 0, Now));
    }
}
