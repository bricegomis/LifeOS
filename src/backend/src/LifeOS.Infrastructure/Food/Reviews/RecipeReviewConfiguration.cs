using LifeOS.Domain.Food.Reviews;
using LifeOS.Domain.Households;
using LifeOS.Domain.Food.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Food.Reviews;

internal sealed class RecipeReviewConfiguration : IEntityTypeConfiguration<RecipeReview>
{
    public void Configure(EntityTypeBuilder<RecipeReview> builder)
    {
        builder.ToTable("recipe_reviews", table =>
        {
            table.HasCheckConstraint("ck_recipe_review_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 10)");
            table.HasCheckConstraint("ck_recipe_review_content", "\"Score\" IS NOT NULL OR NULLIF(BTRIM(\"Comment\"), '') IS NOT NULL");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.HasAlternateKey(r => new { r.Id, r.HouseholdId });
        builder.HasOne<Recipe>().WithMany().HasForeignKey(r => new { r.RecipeId, r.HouseholdId })
            .HasPrincipalKey(r => new { r.Id, r.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(r => new { r.MemberProfileId, r.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HouseholdMember>().WithMany().HasForeignKey(r => new { r.CreatedByUserId, r.HouseholdId })
            .HasPrincipalKey(m => new { m.SupabaseUserId, m.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.RecipeId, r.MemberProfileId }).IsUnique();
    }
}
