using LifeOS.Domain.ComposedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class ComposedMealConfiguration : IEntityTypeConfiguration<ComposedMeal>
{
    public void Configure(EntityTypeBuilder<ComposedMeal> builder)
    {
        builder.ToTable("composed_meals");

        builder.HasKey(meal => meal.Id);
        builder.Property(meal => meal.Id).ValueGeneratedNever();

        builder.Property(meal => meal.HouseholdId).IsRequired();

        builder.Property(meal => meal.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(meal => meal.CreatedAt).IsRequired();
        builder.Property(meal => meal.UpdatedAt).IsRequired();

        // Isolation by household
        builder.HasOne<LifeOS.Domain.Households.Household>()
            .WithMany()
            .HasForeignKey(meal => meal.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(meal => meal.HouseholdId);

        // Navigation for parts
        builder.HasMany<ComposedMealPart>()
            .WithOne()
            .HasForeignKey(part => part.ComposedMealId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
