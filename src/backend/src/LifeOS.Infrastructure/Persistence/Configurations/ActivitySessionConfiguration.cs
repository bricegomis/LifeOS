using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class ActivitySessionConfiguration : IEntityTypeConfiguration<ActivitySession>
{
    public void Configure(EntityTypeBuilder<ActivitySession> builder)
    {
        builder.ToTable("activity_sessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();

        builder.Property(session => session.DayPlanId).IsRequired();
        builder.Property(session => session.Type)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(session => session.Intensity)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(session => session.DurationMinutes).IsRequired();
        builder.Property(session => session.EstimatedEnergyKcal)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(session => session.CreatedAt).IsRequired();
        builder.Property(session => session.UpdatedAt).IsRequired();

        // FK to DayPlan
        builder.HasOne<DayPlan>()
            .WithMany()
            .HasForeignKey(session => session.DayPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(session => session.DayPlanId);
    }
}
