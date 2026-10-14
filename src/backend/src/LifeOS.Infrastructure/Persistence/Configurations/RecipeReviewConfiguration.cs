using LifeOS.Domain.Households;
using LifeOS.Domain.Recipes;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class RecipeReviewConfiguration : IEntityTypeConfiguration<RecipeReview>
{
    public void Configure(EntityTypeBuilder<RecipeReview> builder)
    {
        builder.ToTable("recipe_reviews", table =>
        {
            table.HasCheckConstraint("ck_recipe_review_source", "(\"RecipeId\" IS NULL) <> (\"PlannedRecipeLineId\" IS NULL)");
            table.HasCheckConstraint("ck_recipe_review_target", "(\"MemberProfileId\" IS NULL) <> (\"HouseholdGroupId\" IS NULL)");
            table.HasCheckConstraint("ck_recipe_review_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 10)");
            table.HasCheckConstraint("ck_recipe_review_content", "\"Score\" IS NOT NULL OR NULLIF(BTRIM(\"Comment\"), '') IS NOT NULL");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.HasAlternateKey(r => new { r.Id, r.HouseholdId });
        builder.HasOne<Recipe>().WithMany().HasForeignKey(r => new { r.RecipeId, r.HouseholdId })
            .HasPrincipalKey(r => new { r.Id, r.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlannedRecipeLine>().WithMany().HasForeignKey(r => new { r.PlannedRecipeLineId, r.HouseholdId })
            .HasPrincipalKey(r => new { r.Id, r.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(r => new { r.MemberProfileId, r.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HouseholdGroup>().WithMany().HasForeignKey(r => new { r.HouseholdGroupId, r.HouseholdId })
            .HasPrincipalKey(g => new { g.Id, g.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HouseholdMember>().WithMany().HasForeignKey(r => new { r.CreatedByUserId, r.HouseholdId })
            .HasPrincipalKey(m => new { m.SupabaseUserId, m.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.RecipeId, r.MemberProfileId }).IsUnique()
            .HasFilter("\"RecipeId\" IS NOT NULL AND \"MemberProfileId\" IS NOT NULL");
        builder.HasIndex(r => new { r.RecipeId, r.HouseholdGroupId }).IsUnique()
            .HasFilter("\"RecipeId\" IS NOT NULL AND \"HouseholdGroupId\" IS NOT NULL");
        builder.HasIndex(r => new { r.PlannedRecipeLineId, r.MemberProfileId }).IsUnique()
            .HasFilter("\"PlannedRecipeLineId\" IS NOT NULL AND \"MemberProfileId\" IS NOT NULL");
        builder.HasIndex(r => new { r.PlannedRecipeLineId, r.HouseholdGroupId }).IsUnique()
            .HasFilter("\"PlannedRecipeLineId\" IS NOT NULL AND \"HouseholdGroupId\" IS NOT NULL");
    }
}
