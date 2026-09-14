using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Articles;

/// <summary>
/// Use case: record a new observed price for a grocery article.
/// </summary>
public sealed class AddPriceEntryCommand(IArticleRepository articleRepository)
{
    private readonly IArticleRepository _articleRepository = articleRepository;

    public async Task<GroceryItemDto?> ExecuteAsync(
        Guid householdId,
        Guid articleId,
        Guid storeId,
        decimal price,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        var article = await _articleRepository.GetByIdAsync(householdId, articleId, cancellationToken);

        if (article is null)
        {
            return null;
        }

        article.AddPriceEntry(storeId, price, observedAt);

        await _articleRepository.UpdateAsync(article, cancellationToken);

        return ArticleMapper.ToDto(article);
    }
}
