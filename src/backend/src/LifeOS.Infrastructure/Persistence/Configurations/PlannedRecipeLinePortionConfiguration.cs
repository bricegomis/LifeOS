using LifeOS.Domain.Households;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class PlannedRecipeLinePortionConfiguration : IEntityTypeConfiguration<PlannedRecipeLinePortion>
{
    public void Configure(EntityTypeBuilder<PlannedRecipeLinePortion> builder)
    {
        builder.ToTable("planned_recipe_line_portions", table =>
            table.HasCheckConstraint("ck_planned_recipe_line_portion_servings", "\"Servings\" > 0"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Servings).HasPrecision(12, 4);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(p => new { p.MemberProfileId, p.HouseholdId })
            .HasPrincipalKey(m => new { m.Id, m.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.PlannedRecipeLineId, p.MemberProfileId }).IsUnique();
    }
}
