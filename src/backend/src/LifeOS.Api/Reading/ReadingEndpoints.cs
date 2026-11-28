using LifeOS.Api.Files;
using LifeOS.Api.Households;
using LifeOS.Api.Validation;
using LifeOS.Infrastructure.Persistence;
using LifeOS.Infrastructure.Reading;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Reading;

public static class ReadingModule
{
    /// <summary>Management code shared by the Reading endpoints and the assistant tools (ADR 0022).</summary>
    public static IServiceCollection AddReadingLibraries(this IServiceCollection services) => services
        .AddScoped<ReadingProfiles>()
        .AddScoped<BookLibrary>()
        .AddScoped<BookSeriesLibrary>();

    public static IEndpointRouteBuilder MapReadingEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/api/reading/books").RequireAuthorization().WithTags("Lecture").AddRequestValidation();
        books.MapGet("/", async (string? state, string? q, string? format, string? genre, Guid? seriesId, HouseholdContext household,
                BookLibrary library, CancellationToken ct) =>
            Results.Ok(await library.SearchAsync(household.HouseholdId, state, q, format, genre, seriesId, ct)))
            .Produces<List<BookDto>>().ProducesValidationProblem();
        books.MapGet("/genres", async (HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Results.Ok(await library.GenresAsync(household.HouseholdId, ct))).Produces<List<string>>();
        books.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.GetAsync(household.HouseholdId, id, ct)))
            .Produces<BookDto>().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapPost("/", async (BookCreateRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
        {
            var book = await library.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/reading/books/{book.Id}", book);
        }).Produces<BookDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        books.MapPut("/{id:guid}", async (Guid id, BookRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.UpdateAsync(household.HouseholdId, id, request, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            await library.DeleteAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        books.MapPost("/{id:guid}/start", async (Guid id, BookStartRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.StartAsync(household.HouseholdId, id, request.Day, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapPost("/{id:guid}/finish", async (Guid id, BookFinishRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.FinishAsync(household.HouseholdId, id, request, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapPost("/{id:guid}/stop", async (Guid id, BookStopRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.StopAsync(household.HouseholdId, id, request, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapPost("/{id:guid}/readings", async (Guid id, BookPastReadingRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.AddPastReadingAsync(household.HouseholdId, id, request, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapPut("/{id:guid}/readings/{readingId:guid}", async (Guid id, Guid readingId, BookReadingUpdateRequest request,
                HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.UpdateReadingAsync(household.HouseholdId, id, readingId, request, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapDelete("/{id:guid}/readings/{readingId:guid}", async (Guid id, Guid readingId, HouseholdContext household,
                BookLibrary library, CancellationToken ct) =>
            Found(await library.RemoveReadingAsync(household.HouseholdId, id, readingId, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapPost("/{id:guid}/not-wanted", async (Guid id, BookNotWantedRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.MarkNotWantedAsync(household.HouseholdId, id, request.Reason, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapDelete("/{id:guid}/not-wanted", async (Guid id, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.ClearNotWantedAsync(household.HouseholdId, id, ct)))
            .Produces<BookDto>().ProducesProblem(StatusCodes.Status404NotFound);

        books.MapPost("/{id:guid}/quotes", async (Guid id, BookQuoteRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.AddQuoteAsync(household.HouseholdId, id, request, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapPut("/{id:guid}/quotes/{quoteId:guid}", async (Guid id, Guid quoteId, BookQuoteRequest request, HouseholdContext household,
                BookLibrary library, CancellationToken ct) =>
            Found(await library.UpdateQuoteAsync(household.HouseholdId, id, quoteId, request, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapDelete("/{id:guid}/quotes/{quoteId:guid}", async (Guid id, Guid quoteId, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            Found(await library.RemoveQuoteAsync(household.HouseholdId, id, quoteId, ct)))
            .Produces<BookDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);

        books.MapGet("/{id:guid}/image", async (Guid id, HouseholdContext household, HttpContext context, BookLibrary library,
            BookImageStorage storage, CancellationToken ct) =>
        {
            var (path, contentType) = await library.ImageAsync(household.HouseholdId, id, ct);
            return storage.Serve(context, path, contentType);
        });
        books.MapPut("/{id:guid}/image", async (Guid id, HttpRequest request, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
        {
            var file = await request.ReadFileAsync("image", "Sélectionnez une image.", ct);
            await using var content = file.OpenReadStream();
            return Found(await library.ReplaceImageAsync(household.HouseholdId, id, content, file.Length, file.ContentType, ct));
        }).WithUploadLimit().Produces<BookDto>().ProducesProblem(StatusCodes.Status404NotFound);
        books.MapDelete("/{id:guid}/image", async (Guid id, HouseholdContext household, BookLibrary library, CancellationToken ct) =>
            await library.RemoveImageAsync(household.HouseholdId, id, ct) is null ? Results.NotFound() : Results.NoContent())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        var series = app.MapGroup("/api/reading/series").RequireAuthorization().WithTags("Lecture").AddRequestValidation();
        series.MapGet("/", async (HouseholdContext household, BookSeriesLibrary library, CancellationToken ct) =>
            Results.Ok(await library.ListAsync(household.HouseholdId, ct))).Produces<List<BookSeriesDto>>();
        series.MapGet("/{id:guid}", async (Guid id, HouseholdContext household, BookSeriesLibrary library, CancellationToken ct) =>
            Found(await library.GetAsync(household.HouseholdId, id, ct)))
            .Produces<BookSeriesDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);
        series.MapPost("/", async (BookSeriesRequest request, HouseholdContext household, BookSeriesLibrary library, CancellationToken ct) =>
        {
            var created = await library.CreateAsync(household.HouseholdId, request, ct);
            return Results.Created($"/api/reading/series/{created.Series.Id}", created);
        }).Produces<BookSeriesDetailDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        series.MapPut("/{id:guid}", async (Guid id, BookSeriesRequest request, HouseholdContext household, BookSeriesLibrary library, CancellationToken ct) =>
            Found(await library.UpdateAsync(household.HouseholdId, id, request, ct)))
            .Produces<BookSeriesDetailDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        series.MapDelete("/{id:guid}", async (Guid id, HouseholdContext household, BookSeriesLibrary library, CancellationToken ct) =>
            await library.DeleteAsync(household.HouseholdId, id, ct) ? Results.NoContent() : Results.NotFound())
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    private static IResult Found<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);
}

/// <summary>
/// Who reads. Every book belongs to a profile of the household core (ADR 0022); without a profile, the household's
/// main profile (its first one) is used.
/// </summary>
public sealed class ReadingProfiles(LifeOSDbContext db)
{
    public async Task<Guid> ResolveAsync(Guid householdId, Guid? profileId, CancellationToken ct)
    {
        if (profileId is { } id)
        {
            return await db.MemberProfiles.AnyAsync(p => p.Id == id && p.HouseholdId == householdId, ct)
                ? id
                : throw new ArgumentException("Profil introuvable dans votre foyer.");
        }
        return await db.MemberProfiles.Where(p => p.HouseholdId == householdId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("Aucun profil dans votre foyer.");
    }
}
