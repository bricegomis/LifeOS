using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Articles;

/// <summary>
/// Use case: delete a grocery article belonging to the current user.
/// </summary>
public sealed class DeleteArticleCommand(IArticleRepository articleRepository)
{
    private readonly IArticleRepository _articleRepository = articleRepository;

    public Task<bool> ExecuteAsync(Guid ownerId, Guid articleId, CancellationToken cancellationToken = default)
        => _articleRepository.DeleteAsync(ownerId, articleId, cancellationToken);
}
