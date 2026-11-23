using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Sports;

internal sealed class SportSessionConfiguration : IEntityTypeConfiguration<SportSession>
{
    public void Configure(EntityTypeBuilder<SportSession> builder)
    {
        builder.ToTable("sport_sessions", SportsSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_sport_session_sport", $"\"Sport\" IN ({SportsSchema.SportCodes})");
            t.HasCheckConstraint("ck_sport_session_duration", "\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= 1440");
            t.HasCheckConstraint("ck_sport_session_values",
                "(\"Calories\" IS NULL OR \"Calories\" >= 0) AND (\"DistanceKm\" IS NULL OR \"DistanceKm\" >= 0)" +
                " AND (\"Effort\" IS NULL OR (\"Effort\" >= 1 AND \"Effort\" <= 10))" +
                " AND (\"Feel\" IS NULL OR \"Feel\" IN ('very_weak', 'weak', 'normal', 'strong', 'very_strong'))");
            t.HasCheckConstraint("ck_sport_session_source",
                "(\"Source\" = 'manual' AND \"GarminActivityId\" IS NULL) OR (\"Source\" = 'garmin' AND \"GarminActivityId\" IS NOT NULL)");
            t.HasCheckConstraint("ck_sport_session_times", "\"EndedAt\" > \"StartedAt\"");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Sport).HasMaxLength(30).IsRequired();
        builder.Property(s => s.DistanceKm).HasPrecision(8, 3);
        builder.Property(s => s.Notes).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.Source).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Feel).HasMaxLength(20);
        builder.Property(s => s.AverageSpeedKmh).HasPrecision(7, 2);
        builder.Property(s => s.AerobicTrainingEffect).HasPrecision(3, 1);
        builder.Property(s => s.AnaerobicTrainingEffect).HasPrecision(3, 1);
        builder.Property(s => s.GarminType).HasMaxLength(60);
        builder.Property(s => s.GarminName).HasMaxLength(200);
        builder.Property(s => s.GarminRaw).HasColumnType("jsonb");
        builder.HasOne<Household>().WithMany().HasForeignKey(s => s.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(s => new { s.MemberProfileId, s.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SportTemplate>().WithMany().HasForeignKey(s => new { s.TemplateId, s.HouseholdId })
            .HasPrincipalKey(t => new { t.Id, t.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.HouseholdId, s.MemberProfileId, s.Date });
        builder.HasIndex(s => new { s.HouseholdId, s.MemberProfileId, s.GarminActivityId }).IsUnique()
            .HasFilter("\"GarminActivityId\" IS NOT NULL");
    }
}
