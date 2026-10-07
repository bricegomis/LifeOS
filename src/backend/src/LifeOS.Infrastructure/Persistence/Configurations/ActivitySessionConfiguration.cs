using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class ActivitySessionConfiguration : IEntityTypeConfiguration<ActivitySession>
{
    public void Configure(EntityTypeBuilder<ActivitySession> builder)
    {
        builder.ToTable("activity_sessions", t =>
            t.HasCheckConstraint("ck_activity_time", "(\"StartMinute\" IS NULL AND \"EndMinute\" IS NULL) OR (\"StartMinute\" IS NOT NULL AND \"EndMinute\" IS NOT NULL AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"EndMinute\" > \"StartMinute\")"));
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.DistanceKm).HasPrecision(12, 3);
        builder.HasOne<SportTemplate>().WithMany().HasForeignKey(s => s.SportTemplateId).OnDelete(DeleteBehavior.Restrict);

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
