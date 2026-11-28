using LifeOS.Domain.Common;

namespace LifeOS.Domain.Reading;

/// <summary>
/// A book of the reading library (ADR 0022), owned by a household profile. Its state is never stored: it is
/// computed from its readings (the last one decides) or, without any reading, from the « pas envie » mark.
/// A book has at most one reading in progress. No pages, minutes or sessions in this version.
/// </summary>
public sealed class Book : Entity
{
    public const int MaxTitleLength = 200;
    public const int MaxAuthorsLength = 300;
    public const int MaxNotesLength = 4000;
    public const int MaxGenres = 10;
    public const int MaxGenreLength = 40;
    public const int MaxReasonLength = 300;
    public const int MaxReadings = 50;
    public const int MaxQuotes = 100;
    public const decimal MaxTome = 9999m;

    public static readonly IReadOnlyDictionary<string, string> Formats = new Dictionary<string, string>
    {
        ["paper"] = "Papier",
        ["ereader"] = "Liseuse",
        ["audio"] = "Audio",
        ["comic"] = "BD / manga",
    };

    public static readonly IReadOnlyDictionary<string, string> States = new Dictionary<string, string>
    {
        ["toread"] = "À lire",
        ["reading"] = "En cours",
        ["read"] = "Lu",
        ["stopped"] = "Arrêté",
        ["notwanted"] = "Pas envie",
    };

    private readonly List<BookReading> _readings = [];
    private readonly List<BookQuote> _quotes = [];

    public Guid HouseholdId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public string Title { get; private set; } = "";
    public string Authors { get; private set; } = "";
    public string Format { get; private set; } = "paper";
    public List<string> Genres { get; private set; } = [];
    public Guid? SeriesId { get; private set; }
    public decimal? Tome { get; private set; }
    public string? Notes { get; private set; }
    public bool NotWanted { get; private set; }
    public string? NotWantedReason { get; private set; }
    public string? ImagePath { get; private set; }
    public string? ImageContentType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<BookReading> Readings => _readings;
    public IReadOnlyList<BookQuote> Quotes => _quotes;
    public IEnumerable<BookReading> OrderedReadings => _readings.OrderBy(r => r.Position);
    public IEnumerable<BookQuote> OrderedQuotes => _quotes.OrderBy(q => q.Position);
    public bool HasImage => ImagePath is not null;
    public BookReading? LastReading => _readings.MaxBy(r => r.Position);
    public BookReading? CurrentReading => _readings.SingleOrDefault(r => r.Outcome == "reading");

    /// <summary>toread, reading, read, stopped or notwanted.</summary>
    public string State => LastReading?.Outcome ?? (NotWanted ? "notwanted" : "toread");

    /// <summary>The rating of the latest reading that has one.</summary>
    public int? LastRating => OrderedReadings.Reverse().Select(r => r.Rating).FirstOrDefault(r => r is not null);

    private Book() { }

    public static Book Create(Guid householdId, Guid memberProfileId, string title, string? authors, string format,
        IEnumerable<string>? genres, string? notes, DateTimeOffset now)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("Foyer obligatoire.");
        if (memberProfileId == Guid.Empty) throw new ArgumentException("Profil obligatoire.");
        var book = new Book { Id = Guid.NewGuid(), HouseholdId = householdId, MemberProfileId = memberProfileId, CreatedAt = now };
        book.Update(title, authors, format, genres, notes, now);
        return book;
    }

    public void Update(string title, string? authors, string format, IEnumerable<string>? genres, string? notes, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaxTitleLength)
            throw new ArgumentException($"Titre obligatoire ({MaxTitleLength} caractères maximum).");
        var who = string.IsNullOrWhiteSpace(authors) ? "" : authors.Trim();
        if (who.Length > MaxAuthorsLength) throw new ArgumentException($"Auteurs : {MaxAuthorsLength} caractères maximum.");
        var code = string.IsNullOrWhiteSpace(format) ? "paper" : format.Trim();
        if (!Formats.ContainsKey(code)) throw new ArgumentException("Format : paper, ereader, audio ou comic.");
        var text = Clean(notes, MaxNotesLength, "Notes");
        Title = title.Trim();
        Authors = who;
        Format = code;
        Genres = CleanGenres(genres);
        Notes = text;
        UpdatedAt = now;
    }

    /// <summary>Free household genres: trimmed, without duplicates (case ignored), 10 at most.</summary>
    public static List<string> CleanGenres(IEnumerable<string>? genres)
    {
        var clean = (genres ?? []).Select(g => g?.Trim() ?? "").Where(g => g.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (clean.Count > MaxGenres) throw new ArgumentException($"Un livre porte au plus {MaxGenres} genres.");
        if (clean.Any(g => g.Length > MaxGenreLength)) throw new ArgumentException($"Un genre fait au plus {MaxGenreLength} caractères.");
        return clean;
    }

    /// <summary>The series (of the same household, checked by the caller) and the tome number; no series, no tome.</summary>
    public void SetSeries(Guid? seriesId, decimal? tome, DateTimeOffset now)
    {
        var id = seriesId == Guid.Empty ? null : seriesId;
        if (id is null && tome is not null) throw new ArgumentException("Un numéro de tome demande une série.");
        if (tome is { } t && (t <= 0 || t > MaxTome || decimal.Round(t, 2) != t))
            throw new ArgumentException($"Numéro de tome : de 0,01 à {MaxTome}, deux décimales au plus.");
        SeriesId = id;
        Tome = id is null ? null : tome;
        UpdatedAt = now;
    }

    /// <summary>Starts a new reading (a first one, a re-read or a resumed book); it also lifts « pas envie ».</summary>
    public BookReading Start(DateOnly? day, DateOnly today, DateTimeOffset now)
    {
        if (CurrentReading is not null) throw new ArgumentException("Ce livre est déjà en cours de lecture.");
        var start = day ?? today;
        if (start > today) throw new ArgumentException("Une lecture ne commence pas dans le futur.");
        if (LastReading?.FinishedOn is { } previous && start < previous)
            throw new ArgumentException("Une nouvelle lecture commence après la fin de la précédente.");
        var reading = AddReading(BookReading.Begin(NextReadingPosition(), start));
        NotWanted = false;
        NotWantedReason = null;
        UpdatedAt = now;
        return reading;
    }

    /// <summary>Finishes the reading in progress, with an optional rating 0..10 (0 is a rating) and review.</summary>
    public void Finish(DateOnly? day, int? rating, string? review, DateOnly today, DateTimeOffset now)
    {
        var reading = CurrentReading ?? throw new ArgumentException("Aucune lecture en cours pour ce livre.");
        reading.Close("read", day ?? today, today, rating, review, null);
        UpdatedAt = now;
    }

    /// <summary>Stops the reading in progress, with an optional reason.</summary>
    public void Stop(DateOnly? day, string? reason, DateOnly today, DateTimeOffset now)
    {
        var reading = CurrentReading ?? throw new ArgumentException("Aucune lecture en cours pour ce livre.");
        reading.Close("stopped", day ?? today, today, null, null, reason);
        UpdatedAt = now;
    }

    /// <summary>
    /// A reading already over (« lu en 2020 », an import): read or stopped, dates optional; it goes after the other
    /// readings, so it is refused while one is in progress.
    /// </summary>
    public BookReading AddPastReading(string outcome, DateOnly? startedOn, DateOnly? finishedOn, int? rating, string? review,
        string? reason, DateOnly today, DateTimeOffset now)
    {
        if (CurrentReading is not null) throw new ArgumentException("Terminez ou arrêtez d'abord la lecture en cours.");
        if (outcome is not ("read" or "stopped")) throw new ArgumentException("Issue d'une lecture passée : read ou stopped.");
        var reading = AddReading(BookReading.Past(NextReadingPosition(), outcome, startedOn, finishedOn, rating, review, reason, today));
        NotWanted = false;
        NotWantedReason = null;
        UpdatedAt = now;
        return reading;
    }

    /// <summary>Corrects the dates, rating, review or reason of a reading; its outcome does not change here.</summary>
    public void UpdateReading(Guid readingId, DateOnly? startedOn, DateOnly? finishedOn, int? rating, string? review, string? reason,
        DateOnly today, DateTimeOffset now)
    {
        var reading = FindReading(readingId);
        reading.Correct(startedOn, finishedOn, rating, review, reason, today);
        UpdatedAt = now;
    }

    public void RemoveReading(Guid readingId, DateTimeOffset now)
    {
        _readings.Remove(FindReading(readingId));
        UpdatedAt = now;
    }

    /// <summary>Set aside on purpose; only a book never read.</summary>
    public void MarkNotWanted(string? reason, DateTimeOffset now)
    {
        if (_readings.Count > 0) throw new ArgumentException("Un livre déjà commencé ne passe pas en « Pas envie » : arrêtez la lecture.");
        NotWanted = true;
        NotWantedReason = Clean(reason, MaxReasonLength, "Raison");
        UpdatedAt = now;
    }

    public void ClearNotWanted(DateTimeOffset now)
    {
        NotWanted = false;
        NotWantedReason = null;
        UpdatedAt = now;
    }

    public BookQuote AddQuote(string text, string? place, DateTimeOffset now)
    {
        if (_quotes.Count >= MaxQuotes) throw new ArgumentException($"Un livre garde au plus {MaxQuotes} citations.");
        var quote = new BookQuote(Guid.NewGuid(), _quotes.Count == 0 ? 0 : _quotes.Max(q => q.Position) + 1, text, place);
        _quotes.Add(quote);
        UpdatedAt = now;
        return quote;
    }

    public void UpdateQuote(Guid quoteId, string text, string? place, DateTimeOffset now)
    {
        FindQuote(quoteId).Update(text, place);
        UpdatedAt = now;
    }

    public void RemoveQuote(Guid quoteId, DateTimeOffset now)
    {
        _quotes.Remove(FindQuote(quoteId));
        UpdatedAt = now;
    }

    public void SetImage(string imagePath, string imageContentType)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || imagePath.Length > 500) throw new ArgumentException("Référence d'image invalide.");
        if (imageContentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Format d'image invalide.");
        ImagePath = imagePath;
        ImageContentType = imageContentType;
    }

    public void RemoveImage()
    {
        ImagePath = null;
        ImageContentType = null;
    }

    internal static string? Clean(string? text, int max, string label)
    {
        var value = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (value?.Length > max) throw new ArgumentException($"{label} : {max} caractères maximum.");
        return value;
    }

    private BookReading AddReading(BookReading reading)
    {
        if (_readings.Count >= MaxReadings) throw new ArgumentException($"Un livre garde au plus {MaxReadings} lectures.");
        _readings.Add(reading);
        return reading;
    }

    private int NextReadingPosition() => _readings.Count == 0 ? 0 : _readings.Max(r => r.Position) + 1;

    private BookReading FindReading(Guid id) =>
        _readings.SingleOrDefault(r => r.Id == id) ?? throw new ArgumentException("Lecture introuvable pour ce livre.");

    private BookQuote FindQuote(Guid id) =>
        _quotes.SingleOrDefault(q => q.Id == id) ?? throw new ArgumentException("Citation introuvable pour ce livre.");
}

/// <summary>
/// One reading of a book, in order: in progress, read or stopped. Dates are optional for a past reading (a book read
/// long ago); a rating 0..10 and a review belong to a finished reading, a reason to a stopped one.
/// </summary>
public sealed class BookReading
{
    public const int MaxReviewLength = 4000;

    public Guid Id { get; private set; }
    public int Position { get; private set; }
    public string Outcome { get; private set; } = "reading";
    public DateOnly? StartedOn { get; private set; }
    public DateOnly? FinishedOn { get; private set; }
    public int? Rating { get; private set; }
    public string? Review { get; private set; }
    public string? StopReason { get; private set; }

    private BookReading() { }

    internal static BookReading Begin(int position, DateOnly startedOn) =>
        new() { Id = Guid.NewGuid(), Position = position, Outcome = "reading", StartedOn = startedOn };

    internal static BookReading Past(int position, string outcome, DateOnly? startedOn, DateOnly? finishedOn, int? rating,
        string? review, string? reason, DateOnly today)
    {
        var reading = new BookReading { Id = Guid.NewGuid(), Position = position, Outcome = outcome };
        reading.Correct(startedOn, finishedOn, rating, review, reason, today);
        return reading;
    }

    internal void Close(string outcome, DateOnly finishedOn, DateOnly today, int? rating, string? review, string? reason)
    {
        var previous = Outcome;
        Outcome = outcome;
        try
        {
            Correct(StartedOn, finishedOn, rating, review, reason, today);
        }
        catch (ArgumentException)
        {
            Outcome = previous;
            throw;
        }
    }

    internal void Correct(DateOnly? startedOn, DateOnly? finishedOn, int? rating, string? review, string? reason, DateOnly today)
    {
        if (Outcome == "reading")
        {
            if (startedOn is null) throw new ArgumentException("Une lecture en cours a une date de début.");
            if (finishedOn is not null) throw new ArgumentException("Une lecture en cours n'a pas de date de fin.");
            if (rating is not null || !string.IsNullOrWhiteSpace(review))
                throw new ArgumentException("La note et l'avis se donnent en terminant la lecture.");
        }
        if (startedOn > today || finishedOn > today) throw new ArgumentException("Une lecture ne se date pas dans le futur.");
        if (startedOn is { } s && finishedOn is { } f && f < s) throw new ArgumentException("La fin d'une lecture vient après son début.");
        if (rating is < 0 or > 10) throw new ArgumentException("Note : un entier de 0 à 10.");
        if (Outcome == "stopped" && rating is not null) throw new ArgumentException("Une lecture arrêtée n'est pas notée.");
        StartedOn = startedOn;
        FinishedOn = finishedOn;
        Rating = Outcome == "read" ? rating : null;
        Review = Outcome == "read" ? Book.Clean(review, MaxReviewLength, "Avis") : null;
        StopReason = Outcome == "stopped" ? Book.Clean(reason, Book.MaxReasonLength, "Raison") : null;
    }
}

/// <summary>A quote kept from a book, with an optional place (« p. 42 », « chap. 3 »).</summary>
public sealed class BookQuote
{
    public const int MaxTextLength = 2000;
    public const int MaxPlaceLength = 100;

    public Guid Id { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; } = "";
    public string? Place { get; private set; }

    private BookQuote() { }

    internal BookQuote(Guid id, int position, string text, string? place)
    {
        Id = id;
        Position = position;
        Update(text, place);
    }

    internal void Update(string text, string? place)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxTextLength)
            throw new ArgumentException($"Citation obligatoire ({MaxTextLength} caractères maximum).");
        Text = text.Trim();
        Place = Book.Clean(place, MaxPlaceLength, "Emplacement");
    }
}
