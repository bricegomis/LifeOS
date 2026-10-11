using LifeOS.Domain.FoodItems;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class PlannedRecipeLineIngredientConfiguration : IEntityTypeConfiguration<PlannedRecipeLineIngredient>
{
    public void Configure(EntityTypeBuilder<PlannedRecipeLineIngredient> builder)
    {
        builder.ToTable("planned_recipe_line_ingredients", table =>
            table.HasCheckConstraint("ck_planned_recipe_ingredient_quantity", "\"Quantity\" > 0"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Name).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Unit).HasMaxLength(50).IsRequired();
        builder.Property(i => i.ReferenceUnit).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Quantity).HasPrecision(28, 12);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(i => new { i.FoodItemId, i.HouseholdId })
            .HasPrincipalKey(f => new { f.Id, f.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
    }
}
