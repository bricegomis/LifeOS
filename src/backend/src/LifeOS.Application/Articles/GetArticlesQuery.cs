using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Articles;

/// <summary>
/// Use case: list every grocery article belonging to the current user.
/// </summary>
public sealed class GetArticlesQuery(IArticleRepository articleRepository)
{
    private readonly IArticleRepository _articleRepository = articleRepository;

    public async Task<IReadOnlyList<GroceryItemDto>> ExecuteAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var articles = await _articleRepository.GetAllForOwnerAsync(ownerId, cancellationToken);

        return articles
            .Select(ArticleMapper.ToDto)
            .OrderBy(article => article.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
