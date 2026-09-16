using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class WeekConfiguration : IEntityTypeConfiguration<Week>
{
    public void Configure(EntityTypeBuilder<Week> builder)
    {
        builder.ToTable("weeks");

        builder.HasKey(week => week.Id);
        builder.Property(week => week.Id).ValueGeneratedNever();

        builder.Property(week => week.HouseholdId).IsRequired();
        builder.Property(week => week.StartsOn).IsRequired();

        builder.Property(week => week.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(week => week.CreatedAt).IsRequired();
        builder.Property(week => week.UpdatedAt).IsRequired();

        // Isolation by household
        builder.HasOne<LifeOS.Domain.Households.Household>()
            .WithMany()
            .HasForeignKey(week => week.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(week => week.HouseholdId);
        builder.HasIndex(week => new { week.HouseholdId, week.StartsOn });

        // Navigation for day plans
        builder.HasMany<DayPlan>()
            .WithOne()
            .HasForeignKey(day => day.WeekId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
