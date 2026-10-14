using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class PlannedMealConfiguration : IEntityTypeConfiguration<PlannedMeal>
{
    public void Configure(EntityTypeBuilder<PlannedMeal> builder)
    {
        builder.ToTable("planned_meals", table =>
            table.HasCheckConstraint("ck_meal_time", "(\"StartMinute\" IS NULL AND \"EndMinute\" IS NULL) OR (\"StartMinute\" IS NOT NULL AND \"EndMinute\" IS NOT NULL AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"EndMinute\" > \"StartMinute\")"));
        builder.HasKey(meal => meal.Id);
        builder.Property(meal => meal.Id).ValueGeneratedNever();
        builder.HasAlternateKey(meal => new { meal.Id, meal.HouseholdId });
        builder.Property(meal => meal.HouseholdId).IsRequired();
        builder.Property(meal => meal.CreatedAt).IsRequired();
        builder.Property(meal => meal.UpdatedAt).IsRequired();
        builder.HasOne<DayPlan>().WithMany(day => day.PlannedMeals)
            .HasForeignKey(meal => meal.DayPlanId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(meal => meal.RecipeLines).WithOne()
            .HasForeignKey(line => new { line.PlannedMealId, line.HouseholdId })
            .HasPrincipalKey(meal => new { meal.Id, meal.HouseholdId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(meal => meal.DayPlanId);
    }
}
