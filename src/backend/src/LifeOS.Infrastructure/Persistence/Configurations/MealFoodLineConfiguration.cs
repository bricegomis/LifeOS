using LifeOS.Domain.FoodItems;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class MealFoodLineConfiguration : IEntityTypeConfiguration<MealFoodLine>
{
    public void Configure(EntityTypeBuilder<MealFoodLine> builder)
    {
        builder.ToTable("meal_food_lines", t => t.HasCheckConstraint("ck_line_quantity", "\"Quantity\" > 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Unit).HasMaxLength(50).IsRequired();
        builder.Property(l => l.ReferenceUnit).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(16, 6);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(l => l.FoodItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
