using System.Security.Claims;
using LifeOS.Api.Authentication;
using LifeOS.Api.Contracts;
using LifeOS.Application.Articles;

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
            .RequireAuthorization();

        group.MapGet("/", GetArticlesAsync)
            .WithName("GetArticles");

        group.MapPost("/", CreateArticleAsync)
            .WithName("CreateArticle");

        group.MapPut("/{articleId:guid}", UpdateArticleAsync)
            .WithName("UpdateArticle");

        group.MapDelete("/{articleId:guid}", DeleteArticleAsync)
            .WithName("DeleteArticle");

        group.MapPost("/{articleId:guid}/price-entries", AddPriceEntryAsync)
            .WithName("AddArticlePriceEntry");

        group.MapDelete("/{articleId:guid}/price-entries/{priceEntryId:guid}", DeletePriceEntryAsync)
            .WithName("DeleteArticlePriceEntry");

        return app;
    }

    private static async Task<IResult> GetArticlesAsync(
        ClaimsPrincipal user,
        GetArticlesQuery getArticlesQuery,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        var articles = await getArticlesQuery.ExecuteAsync(ownerId, cancellationToken);

        return Results.Ok(articles);
    }

    private static async Task<IResult> CreateArticleAsync(
        ClaimsPrincipal user,
        ArticleRequest request,
        CreateArticleCommand createArticleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var article = await createArticleCommand.ExecuteAsync(
                ownerId,
                request.Name,
                request.Description,
                request.Unit,
                cancellationToken);

            return Results.Created($"/api/articles/{article.Id}", article);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdateArticleAsync(
        ClaimsPrincipal user,
        Guid articleId,
        ArticleRequest request,
        UpdateArticleCommand updateArticleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var article = await updateArticleCommand.ExecuteAsync(
                ownerId,
                articleId,
                request.Name,
                request.Description,
                request.Unit,
                cancellationToken);

            return article is null ? Results.NotFound() : Results.Ok(article);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> DeleteArticleAsync(
        ClaimsPrincipal user,
        Guid articleId,
        DeleteArticleCommand deleteArticleCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        var deleted = await deleteArticleCommand.ExecuteAsync(ownerId, articleId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AddPriceEntryAsync(
        ClaimsPrincipal user,
        Guid articleId,
        PriceEntryRequest request,
        AddPriceEntryCommand addPriceEntryCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var article = await addPriceEntryCommand.ExecuteAsync(
                ownerId,
                articleId,
                request.StoreId,
                request.Price,
                request.ObservedAt,
                cancellationToken);

            return article is null ? Results.NotFound() : Results.Ok(article);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> DeletePriceEntryAsync(
        ClaimsPrincipal user,
        Guid articleId,
        Guid priceEntryId,
        DeletePriceEntryCommand deletePriceEntryCommand,
        CancellationToken cancellationToken)
    {
        if (!user.TryGetUserId(out var ownerId))
        {
            return Results.Unauthorized();
        }

        var deleted = await deletePriceEntryCommand.ExecuteAsync(ownerId, articleId, priceEntryId, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
