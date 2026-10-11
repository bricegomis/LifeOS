using LifeOS.Domain.FoodItems;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class FoodReviewConfiguration : IEntityTypeConfiguration<FoodReview>
{
    public void Configure(EntityTypeBuilder<FoodReview> builder)
    {
        builder.ToTable("food_reviews", table =>
        {
            table.HasCheckConstraint("ck_food_review_target", "(\"MemberProfileId\" IS NULL) <> (\"HouseholdGroupId\" IS NULL)");
            table.HasCheckConstraint("ck_food_review_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 10)");
            table.HasCheckConstraint("ck_food_review_content", "\"Score\" IS NOT NULL OR NULLIF(BTRIM(\"Comment\"), '') IS NOT NULL");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.HasAlternateKey(r => new { r.Id, r.HouseholdId });
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(r => new { r.FoodItemId, r.HouseholdId })
            .HasPrincipalKey(f => new { f.Id, f.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(r => new { r.MemberProfileId, r.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HouseholdGroup>().WithMany().HasForeignKey(r => new { r.HouseholdGroupId, r.HouseholdId })
            .HasPrincipalKey(g => new { g.Id, g.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HouseholdMember>().WithMany().HasForeignKey(r => new { r.CreatedByUserId, r.HouseholdId })
            .HasPrincipalKey(m => new { m.SupabaseUserId, m.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.FoodItemId, r.MemberProfileId }).IsUnique()
            .HasFilter("\"MemberProfileId\" IS NOT NULL");
        builder.HasIndex(r => new { r.FoodItemId, r.HouseholdGroupId }).IsUnique()
            .HasFilter("\"HouseholdGroupId\" IS NOT NULL");
    }
}
