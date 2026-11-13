using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Sports;

internal sealed class SportTemplateConfiguration : IEntityTypeConfiguration<SportTemplate>
{
    public void Configure(EntityTypeBuilder<SportTemplate> builder)
    {
        builder.ToTable("sport_templates", SportsSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_sport_template_sport", $"\"Sport\" IN ({SportsSchema.SportCodes})");
            t.HasCheckConstraint("ck_sport_duration", "\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= 1440");
            t.HasCheckConstraint("ck_sport_values", "\"Calories\" >= 0 AND (\"DistanceKm\" IS NULL OR \"DistanceKm\" >= 0)");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasAlternateKey(s => new { s.Id, s.HouseholdId });
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Sport).HasMaxLength(30).IsRequired();
        builder.Property(s => s.DistanceKm).HasPrecision(8, 3);
        builder.HasOne<Household>().WithMany().HasForeignKey(s => s.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.HouseholdId);
    }
}
