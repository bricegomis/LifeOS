using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Api.Validation;
using LifeOS.Application.Articles;
using LifeOS.Application.Households;

namespace LifeOS.Api.Endpoints;

/// <summary>
/// Maps the grocery articles endpoints (CRUD + price history management).
/// </summary>
public static class ArticlesEndpoints
{
    public static IEndpointRouteBuilder MapArticlesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/articles")
            .WithTags("Articles")
            .RequireAuthorization()
            .AddRequestValidation();

        group.MapGet("/", GetArticlesAsync)
            .WithName("GetArticles")
            .Produces<List<GroceryItemDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", CreateArticleAsync)
            .WithName("CreateArticle")
            .Produces<GroceryItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{articleId:guid}", UpdateArticleAsync)
            .WithName("UpdateArticle")
            .Produces<GroceryItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{articleId:guid}", DeleteArticleAsync)
            .WithName("DeleteArticle")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{articleId:guid}/price-entries", AddPriceEntryAsync)
            .WithName("AddArticlePriceEntry")
            .Produces<GroceryItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{articleId:guid}/price-entries/{priceEntryId:guid}", DeletePriceEntryAsync)
            .WithName("DeleteArticlePriceEntry")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetArticlesAsync(
        ClaimsPrincipal user,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        GetArticlesQuery getArticlesQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var articles = await getArticlesQuery.ExecuteAsync(householdId, cancellationToken);

        return Results.Ok(articles);
    }

    private static async Task<IResult> CreateArticleAsync(
        ClaimsPrincipal user,
        ArticleRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        CreateArticleCommand createArticleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var article = await createArticleCommand.ExecuteAsync(
                householdId,
                request.Name,
                request.Description,
                request.Unit,
                cancellationToken);

            return Results.Created($"/api/articles/{article.Id}", article);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdateArticleAsync(
        ClaimsPrincipal user,
        Guid articleId,
        ArticleRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        UpdateArticleCommand updateArticleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var article = await updateArticleCommand.ExecuteAsync(
                householdId,
                articleId,
                request.Name,
                request.Description,
                request.Unit,
                cancellationToken);

            return article is null ? Results.NotFound() : Results.Ok(article);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeleteArticleAsync(
        ClaimsPrincipal user,
        Guid articleId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        DeleteArticleCommand deleteArticleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await deleteArticleCommand.ExecuteAsync(householdId, articleId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AddPriceEntryAsync(
        ClaimsPrincipal user,
        Guid articleId,
        PriceEntryRequest request,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        AddPriceEntryCommand addPriceEntryCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);

        try
        {
            var article = await addPriceEntryCommand.ExecuteAsync(
                householdId,
                articleId,
                request.StoreId,
                request.Price,
                request.ObservedAt,
                cancellationToken);

            return article is null ? Results.NotFound() : Results.Ok(article);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> DeletePriceEntryAsync(
        ClaimsPrincipal user,
        Guid articleId,
        Guid priceEntryId,
        ResolveHouseholdForUserQuery resolveHouseholdForUserQuery,
        DeletePriceEntryCommand deletePriceEntryCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var supabaseUserId))
        {
            return Results.Unauthorized();
        }

        var householdId = await resolveHouseholdForUserQuery.ExecuteAsync(supabaseUserId, cancellationToken);
        var deleted = await deletePriceEntryCommand.ExecuteAsync(householdId, articleId, priceEntryId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
