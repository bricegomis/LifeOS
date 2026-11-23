using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Sports;

internal sealed class Vo2MaxMeasureConfiguration : IEntityTypeConfiguration<Vo2MaxMeasure>
{
    public void Configure(EntityTypeBuilder<Vo2MaxMeasure> builder)
    {
        builder.ToTable("vo2max_measures", SportsSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_vo2max_sport", "\"Sport\" IN ('running', 'cycling')");
            t.HasCheckConstraint("ck_vo2max_value", "\"Value\" >= 10 AND \"Value\" <= 100");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Sport).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Value).HasPrecision(4, 1);
        builder.HasOne<Household>().WithMany().HasForeignKey(m => m.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(m => new { m.MemberProfileId, m.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.HouseholdId, m.MemberProfileId, m.Sport, m.Date }).IsUnique();
    }
}
