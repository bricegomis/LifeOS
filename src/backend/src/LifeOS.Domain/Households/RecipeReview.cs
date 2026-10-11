using LifeOS.Domain.Common;

namespace LifeOS.Domain.Households;

public sealed class RecipeReview : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid? RecipeId { get; private set; }
    public Guid? PlannedRecipeLineId { get; private set; }
    public Guid? MemberProfileId { get; private set; }
    public Guid? HouseholdGroupId { get; private set; }
    public int? Score { get; private set; }
    public string? Comment { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RecipeReview() { }

    public static RecipeReview Create(Guid householdId, Guid? recipeId, Guid? lineId,
        Guid? memberProfileId, Guid? groupId, int? score, string? comment, Guid author)
    {
        Validate(householdId, recipeId, lineId, memberProfileId, groupId, score, comment, author);
        var now = DateTimeOffset.UtcNow;
        return new RecipeReview
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, RecipeId = recipeId,
            PlannedRecipeLineId = lineId, MemberProfileId = memberProfileId,
            HouseholdGroupId = groupId, Score = score,
            Comment = Clean(comment), CreatedByUserId = author, CreatedAt = now, UpdatedAt = now,
        };
    }

    public void Update(int? score, string? comment)
    {
        Validate(HouseholdId, RecipeId, PlannedRecipeLineId, MemberProfileId, HouseholdGroupId,
            score, comment, CreatedByUserId);
        Score = score;
        Comment = Clean(comment);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(Guid householdId, Guid? recipeId, Guid? lineId,
        Guid? profileId, Guid? groupId, int? score, string? comment, Guid author)
    {
        if (householdId == Guid.Empty || author == Guid.Empty)
            throw new ArgumentException("Le foyer et l'auteur sont obligatoires.");
        if (recipeId.HasValue == lineId.HasValue)
            throw new ArgumentException("L'avis doit cibler une recette ou une préparation ponctuelle.");
        if (profileId.HasValue == groupId.HasValue)
            throw new ArgumentException("Choisissez une personne ou un groupe, mais pas les deux.");
        if (score is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(score), "La note doit être comprise entre 0 et 10.");
        if (string.IsNullOrWhiteSpace(comment) && score is null)
            throw new ArgumentException("Renseignez une note ou un commentaire.");
        if (comment?.Length > 2000) throw new ArgumentException("Le commentaire ne peut pas dépasser 2 000 caractères.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
