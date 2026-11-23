using LifeOS.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Health;

internal sealed class DailyWellnessConfiguration : IEntityTypeConfiguration<DailyWellness>
{
    public void Configure(EntityTypeBuilder<DailyWellness> builder)
    {
        builder.ToTable("daily_wellness", HealthSchema.Name);
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.SleepQuality).HasMaxLength(20);
        builder.Property(w => w.HrvStatus).HasMaxLength(20);
        builder.Property(w => w.Respiration).HasPrecision(4, 1);
        builder.Property(w => w.SpO2).HasPrecision(4, 1);
        builder.Property(w => w.Raw).HasColumnType("jsonb");
        HealthOwnership.Configure(builder);
        builder.HasIndex(w => new { w.HouseholdId, w.OwnerUserId, w.Date }).IsUnique();
    }
}
