using LifeOS.Domain.Recipes;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class PlannedRecipeLineConfiguration : IEntityTypeConfiguration<PlannedRecipeLine>
{
    public void Configure(EntityTypeBuilder<PlannedRecipeLine> builder)
    {
        builder.ToTable("planned_recipe_lines", table =>
        {
            table.HasCheckConstraint("ck_planned_recipe_line_yield", "\"YieldServings\" > 0");
            table.HasCheckConstraint("ck_planned_recipe_line_portions", "\"PlannedServings\" IS NULL OR \"PlannedServings\" > 0");
            table.HasCheckConstraint("ck_planned_recipe_line_source", "(\"RecipeId\" IS NOT NULL AND \"Name\" IS NOT NULL) OR (\"RecipeId\" IS NULL)");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasAlternateKey(l => new { l.Id, l.HouseholdId });
        builder.Property(l => l.Name).HasMaxLength(200);
        builder.Property(l => l.Instructions).HasMaxLength(10000);
        builder.Property(l => l.YieldServings).HasPrecision(12, 4);
        builder.Property(l => l.PlannedServings).HasPrecision(12, 4);
        builder.HasOne<Recipe>().WithMany().HasForeignKey(l => new { l.RecipeId, l.HouseholdId })
            .HasPrincipalKey(r => new { r.Id, r.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(l => l.Ingredients).WithOne().HasForeignKey(i => new { i.PlannedRecipeLineId, i.HouseholdId })
            .HasPrincipalKey(l => new { l.Id, l.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(l => l.Portions).WithOne().HasForeignKey(p => new { p.PlannedRecipeLineId, p.HouseholdId })
            .HasPrincipalKey(l => new { l.Id, l.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => new { l.PlannedMealId, l.Position }).IsUnique();
        builder.HasIndex(l => new { l.Id, l.HouseholdId });
    }
}
