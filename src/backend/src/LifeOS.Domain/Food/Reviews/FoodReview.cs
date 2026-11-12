using LifeOS.Domain.Common;
using LifeOS.Domain.Households;

namespace LifeOS.Domain.Food.Reviews;

public sealed class FoodReview : Entity
{
    public Guid HouseholdId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public Guid MemberProfileId { get; private set; }
    public int? Score { get; private set; }
    public string? Comment { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FoodReview() { }

    public static FoodReview Create(Guid householdId, Guid foodItemId, Guid memberProfileId, int? score, string? comment, Guid createdBy)
    {
        Validate(householdId, foodItemId, memberProfileId, score, comment, createdBy);
        var now = DateTimeOffset.UtcNow;
        return new FoodReview
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, FoodItemId = foodItemId,
            MemberProfileId = memberProfileId, Score = score,
            Comment = Clean(comment), CreatedByUserId = createdBy, CreatedAt = now, UpdatedAt = now,
        };
    }

    public void Update(int? score, string? comment)
    {
        Validate(HouseholdId, FoodItemId, MemberProfileId, score, comment, CreatedByUserId);
        Score = score;
        Comment = Clean(comment);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(Guid householdId, Guid foodId, Guid profileId, int? score, string? comment, Guid author)
    {
        if (householdId == Guid.Empty || foodId == Guid.Empty || author == Guid.Empty)
            throw new ArgumentException("Le foyer, l'aliment et l'auteur sont obligatoires.");
        if (profileId == Guid.Empty)
            throw new ArgumentException("L'avis doit être rattaché à un profil du foyer.");
        if (score is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(score), "La note doit être comprise entre 0 et 10.");
        if (string.IsNullOrWhiteSpace(comment) && score is null)
            throw new ArgumentException("Renseignez une note ou un commentaire.");
        if (comment?.Length > 2000) throw new ArgumentException("Le commentaire ne peut pas dépasser 2 000 caractères.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
