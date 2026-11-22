using LifeOS.Domain.Health;
using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Health;

internal sealed class SupplementConfiguration : IEntityTypeConfiguration<Supplement>
{
    public void Configure(EntityTypeBuilder<Supplement> builder)
    {
        builder.ToTable("supplements", HealthSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_supplement_unit", "\"Unit\" IN ('gelule', 'comprime', 'g', 'ml', 'goutte', 'dose')");
            t.HasCheckConstraint("ck_supplement_moment", "\"Moment\" IN ('matin', 'midi', 'soir', 'choix')");
            t.HasCheckConstraint("ck_supplement_dose", "\"Dose\" > 0 AND \"PackageContent\" > 0");
            t.HasCheckConstraint("ck_supplement_price", "\"PackagePrice\" IS NULL OR \"PackagePrice\" >= 0");
            t.HasCheckConstraint("ck_supplement_image", "(\"ImagePath\" IS NULL) = (\"ImageContentType\" IS NULL)");
            t.HasCheckConstraint("ck_supplement_period", "\"PeriodKind\" IN ('year', 'cure')");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(Supplement.MaxNameLength).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(Supplement.MaxNotesLength);
        builder.Property(s => s.Dose).HasPrecision(10, 3);
        builder.Property(s => s.Unit).HasMaxLength(10).IsRequired();
        builder.Property(s => s.Moment).HasMaxLength(10).IsRequired();
        builder.Property(s => s.PackageContent).HasPrecision(12, 3);
        builder.Property(s => s.PackagePrice).HasPrecision(10, 2);
        builder.Property(s => s.Shop).HasMaxLength(200);
        builder.Property(s => s.ImagePath).HasMaxLength(500);
        builder.Property(s => s.ImageContentType).HasMaxLength(100);
        builder.Ignore(s => s.HasImage);
        builder.Ignore(s => s.MonthlyCost);
        builder.Ignore(s => s.OrderedLinks);
        builder.ComplexProperty(s => s.Period, period =>
        {
            period.Property(p => p.Kind).HasColumnName("PeriodKind").HasMaxLength(10).IsRequired();
            period.Property(p => p.StartOn).HasColumnName("PeriodStartOn");
            period.Property(p => p.CureLength).HasColumnName("CureLength");
            period.Property(p => p.CureUnit).HasColumnName("CureUnit").HasMaxLength(10).IsRequired();
            period.Property(p => p.PauseLength).HasColumnName("PauseLength");
            period.Property(p => p.PauseUnit).HasColumnName("PauseUnit").HasMaxLength(10).IsRequired();
        });
        HealthOwnership.Configure(builder);
        builder.OwnsMany(s => s.Intakes, intakes =>
        {
            intakes.ToTable("supplement_intakes", HealthSchema.Name);
            intakes.WithOwner().HasForeignKey("SupplementId");
            intakes.HasKey("SupplementId", nameof(SupplementIntake.Day));
        });
        builder.Navigation(s => s.Intakes).HasField("_intakes").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(s => s.Links, links =>
        {
            links.ToTable("supplement_links", HealthSchema.Name);
            links.WithOwner().HasForeignKey("SupplementId");
            links.HasKey("SupplementId", nameof(SupplementLink.Position));
            links.Property(l => l.Label).HasMaxLength(100).IsRequired();
            links.Property(l => l.Url).HasMaxLength(SupplementLink.MaxUrlLength).IsRequired();
        });
        builder.Navigation(s => s.Links).HasField("_links").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// Health rows belong to a household, an account and a profile (ADR 0018): foreign keys to the
/// household core, and an index on the owner, since every query filters on it.
/// </summary>
internal static class HealthOwnership
{
    /// <summary>Expects the properties <c>HouseholdId</c>, <c>OwnerUserId</c> and <c>MemberProfileId</c>.</summary>
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : class
    {
        builder.HasOne<Household>().WithMany().HasForeignKey("HouseholdId").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey("OwnerUserId").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey("MemberProfileId", "HouseholdId")
            .HasPrincipalKey(nameof(MemberProfile.Id), nameof(MemberProfile.HouseholdId)).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex("HouseholdId", "OwnerUserId");
    }
}
