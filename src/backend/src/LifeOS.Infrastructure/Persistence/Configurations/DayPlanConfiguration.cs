using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class DayPlanConfiguration : IEntityTypeConfiguration<DayPlan>
{
    public void Configure(EntityTypeBuilder<DayPlan> builder)
    {
        builder.ToTable("day_plans");

        builder.HasKey(day => day.Id);
        builder.Property(day => day.Id).ValueGeneratedNever();

        builder.Property(day => day.WeekId).IsRequired();
        builder.Property(day => day.Date).IsRequired();

        builder.Property(day => day.WorkContext)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(day => day.BikeCommute).IsRequired();

        // FK to Week
        builder.HasOne<Week>()
            .WithMany()
            .HasForeignKey(day => day.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(day => day.WeekId);
        builder.HasIndex(day => day.Date);

        // Navigation for planned meals
        builder.HasMany<PlannedMeal>()
            .WithOne()
            .HasForeignKey(meal => meal.DayPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
