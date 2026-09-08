using LifeOS.Application.Common.Interfaces;
using LifeOS.Domain.Articles;

namespace LifeOS.Application.Articles;

/// <summary>
/// Use case: create a new grocery article for the current user.
/// </summary>
public sealed class CreateArticleCommand(IArticleRepository articleRepository)
{
    private readonly IArticleRepository _articleRepository = articleRepository;

    public async Task<GroceryItemDto> ExecuteAsync(
        Guid ownerId,
        string name,
        string description,
        string unit,
        CancellationToken cancellationToken = default)
    {
        var article = GroceryItem.Create(ownerId, name, description, ArticleMapper.ParseUnit(unit));

        await _articleRepository.AddAsync(article, cancellationToken);

        return ArticleMapper.ToDto(article);
    }
}
