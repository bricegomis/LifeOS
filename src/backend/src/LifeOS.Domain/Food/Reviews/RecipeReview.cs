using LifeOS.Domain.Common;
using LifeOS.Domain.Households;

namespace LifeOS.Domain.Food.Reviews;

public sealed class RecipeReview : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid RecipeId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public int? Score { get; private set; }
    public string? Comment { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RecipeReview() { }

    public static RecipeReview Create(Guid householdId, Guid recipeId,
        Guid memberProfileId, int? score, string? comment, Guid author)
    {
        Validate(householdId, recipeId, memberProfileId, score, comment, author);
        var now = DateTimeOffset.UtcNow;
        return new RecipeReview
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, RecipeId = recipeId,
            MemberProfileId = memberProfileId, Score = score,
            Comment = Clean(comment), CreatedByUserId = author, CreatedAt = now, UpdatedAt = now,
        };
    }

    public void Update(int? score, string? comment)
    {
        Validate(HouseholdId, RecipeId, MemberProfileId, score, comment, CreatedByUserId);
        Score = score;
        Comment = Clean(comment);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(Guid householdId, Guid recipeId,
        Guid profileId, int? score, string? comment, Guid author)
    {
        if (householdId == Guid.Empty || author == Guid.Empty)
            throw new ArgumentException("Le foyer et l'auteur sont obligatoires.");
        if (recipeId == Guid.Empty)
            throw new ArgumentException("L'avis doit cibler une recette.");
        if (profileId == Guid.Empty)
            throw new ArgumentException("L'avis doit être rattaché à un profil du foyer.");
        if (score is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(score), "La note doit être comprise entre 0 et 10.");
        if (string.IsNullOrWhiteSpace(comment) && score is null)
            throw new ArgumentException("Renseignez une note ou un commentaire.");
        if (comment?.Length > 2000) throw new ArgumentException("Le commentaire ne peut pas dépasser 2 000 caractères.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
