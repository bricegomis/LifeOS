using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class PlannedMealConfiguration : IEntityTypeConfiguration<PlannedMeal>
{
    public void Configure(EntityTypeBuilder<PlannedMeal> builder)
    {
        builder.ToTable("planned_meals");

        builder.HasKey(meal => meal.Id);
        builder.Property(meal => meal.Id).ValueGeneratedNever();

        builder.Property(meal => meal.DayPlanId).IsRequired();

        builder.Property(meal => meal.MealType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(meal => meal.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(meal => meal.ComposedMealId).IsRequired(false);
        builder.Property(meal => meal.RecipeId).IsRequired(false);

        builder.Property(meal => meal.CreatedAt).IsRequired();
        builder.Property(meal => meal.UpdatedAt).IsRequired();

        // FK to DayPlan
        builder.HasOne<DayPlan>()
            .WithMany()
            .HasForeignKey(meal => meal.DayPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to ComposedMeal (optional)
        builder.HasOne<LifeOS.Domain.ComposedMeals.ComposedMeal>()
            .WithMany()
            .HasForeignKey(meal => meal.ComposedMealId)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to Recipe (optional)
        builder.HasOne<LifeOS.Domain.Recipes.Recipe>()
            .WithMany()
            .HasForeignKey(meal => meal.RecipeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(meal => meal.DayPlanId);
        builder.HasIndex(meal => meal.ComposedMealId);
        builder.HasIndex(meal => meal.RecipeId);

        // Navigation for parts
        builder.HasMany<PlannedMealPart>()
            .WithOne()
            .HasForeignKey(part => part.PlannedMealId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
