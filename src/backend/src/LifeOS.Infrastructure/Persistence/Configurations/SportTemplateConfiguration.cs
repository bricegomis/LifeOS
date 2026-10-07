using LifeOS.Domain.Households;
using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class SportTemplateConfiguration : IEntityTypeConfiguration<SportTemplate>
{
    public void Configure(EntityTypeBuilder<SportTemplate> builder)
    {
        builder.ToTable("sport_templates", t =>
        {
            t.HasCheckConstraint("ck_sport_duration", "\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= 1440");
            t.HasCheckConstraint("ck_sport_values", "\"Calories\" >= 0 AND (\"DistanceKm\" IS NULL OR \"DistanceKm\" >= 0)");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Sport).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Intensity).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Calories).HasPrecision(12, 2);
        builder.Property(s => s.DistanceKm).HasPrecision(12, 3);
        builder.HasOne<Household>().WithMany().HasForeignKey(s => s.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.HouseholdId);
    }
}
