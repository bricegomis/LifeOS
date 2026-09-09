using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Articles;

/// <summary>
/// Use case: update the details of an existing grocery article.
/// </summary>
public sealed class UpdateArticleCommand(IArticleRepository articleRepository)
{
    private readonly IArticleRepository _articleRepository = articleRepository;

    public async Task<GroceryItemDto?> ExecuteAsync(
        Guid ownerId,
        Guid articleId,
        string name,
        string description,
        string unit,
        CancellationToken cancellationToken = default)
    {
        var article = await _articleRepository.GetByIdAsync(ownerId, articleId, cancellationToken);

        if (article is null)
        {
            return null;
        }

        article.UpdateDetails(name, description, ArticleMapper.ParseUnit(unit));

        return ArticleMapper.ToDto(article);
    }
}
