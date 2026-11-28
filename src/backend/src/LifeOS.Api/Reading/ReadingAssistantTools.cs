using System.ComponentModel;
using LifeOS.Api.Assistant;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Reading;

/// <summary>
/// Reading tools for the assistant (ADR 0011, ADR 0022): books, their readings and quotes, series, through the same
/// libraries as the endpoints. Recommending what to read next is the assistant's job, from these reads; the API calls
/// no book database.
/// </summary>
[McpServerToolType]
public sealed class ReadingAssistantTools
{
    public const string StateHelp = "États : toread (à lire), reading (en cours), read (lu), stopped (arrêté), notwanted (pas envie : " +
        "connu et écarté exprès, à ne plus proposer). L'état se déduit des lectures : la dernière décide.";
    public const string FormatHelp = "Format : paper (papier, par défaut), ereader (liseuse), audio ou comic (BD / manga).";
    public const string GenresHelp = "Genres libres du foyer (SF, Polar, Essai…), 10 au plus ; réutilisez l'orthographe de list_book_genres.";
    public const string DuplicateHelp = "Avant de créer un livre, cherchez-le avec search_books (titre ou auteur) : en cas de doute sur un " +
        "doublon, demandez à l'utilisateur plutôt que de créer.";
    public const string RatingHelp = "Note entière de 0 à 10 (0 est une note) ; vide = non noté.";

    [McpServerTool(Name = "search_books", ReadOnly = true, Title = "Chercher des livres")]
    [Description("Cherche dans la bibliothèque de lecture du foyer (titre, auteurs, série, genres, sans tenir compte des accents ni " +
        "des majuscules ; chaque mot doit être trouvé). Chaque livre vient avec ses lectures (dates, note, avis) et ses citations. " +
        "En cours d'abord, puis à lire, lus, arrêtés, pas envie. " + StateHelp)]
    public static async Task<List<BookDto>> SearchBooks(IHttpContextAccessor accessor, BookLibrary books,
        [Description("Texte cherché ; vide = tous.")] string? query = null,
        [Description("all (par défaut : tous sauf pas envie), any (tous), toread, reading, read, stopped ou notwanted.")] string? state = null,
        [Description(FormatHelp)] string? format = null,
        string? genre = null, Guid? seriesId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await books.SearchAsync(household.HouseholdId, state, query, format, genre, seriesId, ct);
    }

    [McpServerTool(Name = "get_book", ReadOnly = true, Title = "Livre")]
    [Description("Un livre avec ses lectures, ses citations et ses notes.")]
    public static async Task<BookDto> GetBook(IHttpContextAccessor accessor, BookLibrary books, Guid bookId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.GetAsync(household.HouseholdId, bookId, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "list_book_genres", ReadOnly = true, Title = "Genres des livres")]
    [Description("Les genres déjà utilisés par les livres du foyer.")]
    public static async Task<List<string>> ListBookGenres(IHttpContextAccessor accessor, BookLibrary books, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await books.GenresAsync(household.HouseholdId, ct);
    }

    [McpServerTool(Name = "create_book", Title = "Ajouter un livre")]
    [Description("Ajoute un livre à la bibliothèque du profil principal. Pour un livre déjà lu, donnez state = read avec, si vous " +
        "les connaissez, finishedOn, rating et review (« lu en 2020, 9/10 » : finishedOn = 2020-01-01). Pour importer une liste collée " +
        "par l'utilisateur, appelez cet outil une fois par livre. " + DuplicateHelp + " " + FormatHelp)]
    public static async Task<BookDto> CreateBook(IHttpContextAccessor accessor, BookLibrary books,
        string title,
        [Description("Auteurs, séparés par des virgules.")] string? authors = null,
        [Description(FormatHelp)] string? format = null,
        [Description(GenresHelp)] List<string>? genres = null,
        string? notes = null,
        [Description("toread (par défaut), reading (commencé aujourd'hui), read (déjà lu) ou notwanted.")] string? state = null,
        [Description("Série du foyer (list_book_series, create_book_series).")] Guid? seriesId = null,
        [Description("Numéro du tome dans la série (2.5 permis).")] decimal? tome = null,
        [Description("Pour state = read : jour de fin (aaaa-mm-jj), jamais dans le futur.")] DateOnly? finishedOn = null,
        [Description("Pour state = read : " + RatingHelp)] int? rating = null,
        [Description("Pour state = read : l'avis de l'utilisateur.")] string? review = null,
        [Description("Adresse https de la couverture (JPEG, PNG ou WebP, 10 Mo maximum) ; l'image elle-même.")] string? coverUrl = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var code = string.IsNullOrWhiteSpace(state) ? "toread" : state.Trim();
        var pastRead = code == "read" && (finishedOn is not null || rating is not null || !string.IsNullOrWhiteSpace(review));
        var book = await books.CreateAsync(household.HouseholdId, AssistantRequest.Validated(
            new BookCreateRequest(title, authors, format, genres, notes, seriesId, tome, pastRead ? "toread" : code)), ct);
        if (pastRead)
            book = (await books.AddPastReadingAsync(household.HouseholdId, book.Id,
                AssistantRequest.Validated(new BookPastReadingRequest("read", null, finishedOn, rating, review, null)), ct)).OrNotFound("Livre");
        if (string.IsNullOrWhiteSpace(coverUrl)) return book;
        return (await books.ReplaceImageFromUrlAsync(household.HouseholdId, book.Id, coverUrl, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "update_book", Idempotent = true, Title = "Modifier un livre")]
    [Description("Remplace la fiche d'un livre (un champ omis devient vide ; le format omis est gardé ; les lectures et citations ne " +
        "changent pas). " + FormatHelp)]
    public static async Task<BookDto> UpdateBook(IHttpContextAccessor accessor, BookLibrary books,
        Guid bookId, string title, string? authors = null, [Description(FormatHelp)] string? format = null,
        [Description(GenresHelp)] List<string>? genres = null, string? notes = null,
        [Description("Série du foyer ; vide = sans série.")] Guid? seriesId = null, decimal? tome = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new BookRequest(title, authors, format, genres, notes, seriesId, tome));
        return (await books.UpdateAsync(household.HouseholdId, bookId, request, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "start_book", Title = "Commencer un livre")]
    [Description("Commence une lecture : une première lecture, une relecture d'un livre lu ou la reprise d'un livre arrêté. Retire " +
        "« pas envie ». Une seule lecture en cours par livre.")]
    public static async Task<BookDto> StartBook(IHttpContextAccessor accessor, BookLibrary books, Guid bookId,
        [Description("Jour de début (aaaa-mm-jj) ; aujourd'hui par défaut, jamais dans le futur.")] DateOnly? day = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.StartAsync(household.HouseholdId, bookId, day, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "finish_book", Title = "Livre terminé")]
    [Description("Termine la lecture en cours d'un livre, avec une note et un avis facultatifs. " + RatingHelp)]
    public static async Task<BookDto> FinishBook(IHttpContextAccessor accessor, BookLibrary books, Guid bookId,
        [Description("Jour de fin ; aujourd'hui par défaut.")] DateOnly? day = null,
        [Description(RatingHelp)] int? rating = null, string? review = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.FinishAsync(household.HouseholdId, bookId, AssistantRequest.Validated(new BookFinishRequest(day, rating, review)), ct))
            .OrNotFound("Livre");
    }

    [McpServerTool(Name = "stop_book", Title = "Arrêter un livre")]
    [Description("Arrête la lecture en cours d'un livre (abandonné en route), avec une raison facultative ; il pourra être repris.")]
    public static async Task<BookDto> StopBook(IHttpContextAccessor accessor, BookLibrary books, Guid bookId,
        DateOnly? day = null, string? reason = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.StopAsync(household.HouseholdId, bookId, AssistantRequest.Validated(new BookStopRequest(day, reason)), ct))
            .OrNotFound("Livre");
    }

    [McpServerTool(Name = "add_book_reading", Title = "Ajouter une lecture passée")]
    [Description("Ajoute une lecture déjà finie (read) ou arrêtée (stopped) à un livre, après les autres ; dates facultatives. " +
        "Refusé tant qu'une lecture est en cours. " + RatingHelp)]
    public static async Task<BookDto> AddBookReading(IHttpContextAccessor accessor, BookLibrary books, Guid bookId,
        [Description("read ou stopped.")] string outcome, DateOnly? startedOn = null, DateOnly? finishedOn = null,
        [Description(RatingHelp)] int? rating = null, string? review = null,
        [Description("Pour stopped : la raison.")] string? reason = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new BookPastReadingRequest(outcome, startedOn, finishedOn, rating, review, reason));
        return (await books.AddPastReadingAsync(household.HouseholdId, bookId, request, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "update_book_reading", Idempotent = true, Title = "Corriger une lecture")]
    [Description("Corrige les dates, la note, l'avis ou la raison d'une lecture (un champ omis devient vide) ; son issue ne change pas.")]
    public static async Task<BookDto> UpdateBookReading(IHttpContextAccessor accessor, BookLibrary books, Guid bookId, Guid readingId,
        DateOnly? startedOn = null, DateOnly? finishedOn = null, [Description(RatingHelp)] int? rating = null, string? review = null,
        string? reason = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new BookReadingUpdateRequest(startedOn, finishedOn, rating, review, reason));
        return (await books.UpdateReadingAsync(household.HouseholdId, bookId, readingId, request, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "remove_book_reading", Destructive = true, Title = "Retirer une lecture")]
    [Description("Retire une lecture d'un livre (une erreur de saisie) ; l'état du livre suit sa nouvelle dernière lecture.")]
    public static async Task<BookDto> RemoveBookReading(IHttpContextAccessor accessor, BookLibrary books, Guid bookId, Guid readingId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.RemoveReadingAsync(household.HouseholdId, bookId, readingId, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "set_book_not_wanted", Title = "Pas envie")]
    [Description("Range un livre jamais commencé dans « Pas envie » (connu et écarté exprès), avec une raison facultative, ou l'en " +
        "retire (wanted = false : il revient dans « À lire »). Ne proposez plus un livre « pas envie ».")]
    public static async Task<BookDto> SetBookNotWanted(IHttpContextAccessor accessor, BookLibrary books, Guid bookId,
        bool notWanted = true, string? reason = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var book = notWanted
            ? await books.MarkNotWantedAsync(household.HouseholdId, bookId, AssistantRequest.Validated(new BookNotWantedRequest(reason)).Reason, ct)
            : await books.ClearNotWantedAsync(household.HouseholdId, bookId, ct);
        return book.OrNotFound("Livre");
    }

    [McpServerTool(Name = "add_book_quote", Title = "Ajouter une citation")]
    [Description("Garde une citation d'un livre, avec son emplacement facultatif (« p. 42 », « chap. 3 »).")]
    public static async Task<BookDto> AddBookQuote(IHttpContextAccessor accessor, BookLibrary books, Guid bookId, string text,
        string? place = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.AddQuoteAsync(household.HouseholdId, bookId, AssistantRequest.Validated(new BookQuoteRequest(text, place)), ct))
            .OrNotFound("Livre");
    }

    [McpServerTool(Name = "remove_book_quote", Destructive = true, Title = "Retirer une citation")]
    [Description("Retire une citation d'un livre.")]
    public static async Task<BookDto> RemoveBookQuote(IHttpContextAccessor accessor, BookLibrary books, Guid bookId, Guid quoteId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.RemoveQuoteAsync(household.HouseholdId, bookId, quoteId, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "set_book_cover_from_url", Title = "Couverture d'un livre")]
    [Description("Télécharge la couverture d'un livre depuis une adresse https (JPEG, PNG ou WebP, 10 Mo maximum ; l'image elle-même, " +
        "pas la page qui la contient) et remplace l'ancienne.")]
    public static async Task<BookDto> SetBookCoverFromUrl(IHttpContextAccessor accessor, BookLibrary books, Guid bookId, string url,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.ReplaceImageFromUrlAsync(household.HouseholdId, bookId, url, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "remove_book_cover", Destructive = true, Title = "Retirer une couverture")]
    [Description("Retire la couverture d'un livre.")]
    public static async Task<BookDto> RemoveBookCover(IHttpContextAccessor accessor, BookLibrary books, Guid bookId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await books.RemoveImageAsync(household.HouseholdId, bookId, ct)).OrNotFound("Livre");
    }

    [McpServerTool(Name = "list_book_series", ReadOnly = true, Title = "Séries de livres")]
    [Description("Les séries du foyer avec le nombre de livres, de lus, d'à lire et les tomes manquants (connus quand le nombre de tomes est saisi).")]
    public static async Task<List<BookSeriesDto>> ListBookSeries(IHttpContextAccessor accessor, BookSeriesLibrary series, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await series.ListAsync(household.HouseholdId, ct);
    }

    [McpServerTool(Name = "get_book_series", ReadOnly = true, Title = "Série de livres")]
    [Description("Une série avec ses tomes dans l'ordre, lus, à lire et manquants.")]
    public static async Task<BookSeriesDetailDto> GetBookSeries(IHttpContextAccessor accessor, BookSeriesLibrary series, Guid seriesId,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await series.GetAsync(household.HouseholdId, seriesId, ct)).OrNotFound("Série");
    }

    [McpServerTool(Name = "create_book_series", Title = "Créer une série")]
    [Description("Crée une série de livres (nom unique dans le foyer) ; donnez son nombre de tomes s'il est connu, pour voir les tomes manquants. " +
        "Cherchez-la d'abord avec list_book_series.")]
    public static async Task<BookSeriesDetailDto> CreateBookSeries(IHttpContextAccessor accessor, BookSeriesLibrary series, string name,
        [Description("Nombre de tomes, de 1 à 999.")] int? totalTomes = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return await series.CreateAsync(household.HouseholdId, AssistantRequest.Validated(new BookSeriesRequest(name, totalTomes)), ct);
    }

    [McpServerTool(Name = "update_book_series", Idempotent = true, Title = "Modifier une série")]
    [Description("Renomme une série ou change son nombre de tomes (omis = inconnu).")]
    public static async Task<BookSeriesDetailDto> UpdateBookSeries(IHttpContextAccessor accessor, BookSeriesLibrary series, Guid seriesId,
        string name, int? totalTomes = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await series.UpdateAsync(household.HouseholdId, seriesId, AssistantRequest.Validated(new BookSeriesRequest(name, totalTomes)), ct))
            .OrNotFound("Série");
    }
}
