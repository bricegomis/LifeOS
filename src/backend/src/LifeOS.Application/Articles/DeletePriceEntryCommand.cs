using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Articles;

/// <summary>
/// Use case: remove a previously recorded price observation from a grocery article.
/// </summary>
public sealed class DeletePriceEntryCommand(IArticleRepository articleRepository)
{
    private readonly IArticleRepository _articleRepository = articleRepository;

    public async Task<bool> ExecuteAsync(
        Guid householdId,
        Guid articleId,
        Guid priceEntryId,
        CancellationToken cancellationToken = default)
    {
        var article = await _articleRepository.GetByIdAsync(householdId, articleId, cancellationToken);

        if (article is null || !article.RemovePriceEntry(priceEntryId))
        {
            return false;
        }

        await _articleRepository.UpdateAsync(article, cancellationToken);

        return true;
    }
}
