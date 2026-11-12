using System.ComponentModel;
using LifeOS.Api.Assistant;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Food.Reviews;

/// <summary>Review tools for the assistant (ADR 0011): same code as the endpoints, through <see cref="ReviewLibrary"/>.</summary>
[McpServerToolType]
public sealed class ReviewAssistantTools
{
    [McpServerTool(Name = "list_reviews", ReadOnly = true, Title = "Avis")]
    [Description("Liste les avis des profils du foyer sur un aliment ou une recette (donnez l'un des deux).")]
    public static async Task<List<ReviewDto>> ListReviews(IHttpContextAccessor accessor, ReviewLibrary reviews,
        Guid? foodId = null, Guid? recipeId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (await reviews.ListAsync(household.HouseholdId, Target(foodId, recipeId), ct)).OrNotFound("Aliment ou recette");
    }

    [McpServerTool(Name = "save_review", Idempotent = true, Title = "Donner un avis")]
    [Description("Crée ou remplace l'avis d'un profil du foyer (get_household) sur un aliment ou une recette (donnez l'un des deux). " +
        "Une note de 0 à 10 et/ou un commentaire ; 0 est une vraie note, différente de « non noté » (vide).")]
    public static async Task<ReviewDto> SaveReview(IHttpContextAccessor accessor, ReviewLibrary reviews,
        [Description("Profil du foyer qui donne l'avis (get_household).")] Guid profileId,
        Guid? foodId = null, Guid? recipeId = null,
        [Description("Note entière de 0 à 10.")] int? score = null,
        string? comment = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var request = AssistantRequest.Validated(new SaveReviewRequest(profileId, score, comment));
        return (await reviews.SaveAsync(household, Target(foodId, recipeId), request, ct)).OrNotFound("Aliment ou recette actif");
    }

    [McpServerTool(Name = "delete_review", Destructive = true, Title = "Supprimer un avis")]
    [Description("Supprime un avis (identifiant donné par list_reviews) d'un aliment ou d'une recette (donnez l'un des deux).")]
    public static async Task<string> DeleteReview(IHttpContextAccessor accessor, ReviewLibrary reviews, Guid reviewId,
        Guid? foodId = null, Guid? recipeId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        if (!await reviews.DeleteAsync(household.HouseholdId, Target(foodId, recipeId), reviewId, ct)) throw AssistantRequest.NotFound("Avis");
        return "Avis supprimé.";
    }

    private static ReviewTarget Target(Guid? foodId, Guid? recipeId) => (foodId, recipeId) switch
    {
        ({ } food, null) => ReviewTarget.Food(food),
        (null, { } recipe) => ReviewTarget.Recipe(recipe),
        _ => throw new ArgumentException("Donnez soit foodId, soit recipeId."),
    };
}
