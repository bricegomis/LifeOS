using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Households;

internal sealed class ImportTokenConfiguration : IEntityTypeConfiguration<ImportToken>
{
    public void Configure(EntityTypeBuilder<ImportToken> builder)
    {
        builder.ToTable("import_tokens", "households");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Hash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Hint).HasMaxLength(8).IsRequired();
        builder.HasIndex(t => t.Hash).IsUnique();
        builder.HasOne<Household>().WithMany().HasForeignKey(t => t.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(t => new { t.MemberProfileId, t.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GarminLinkConfiguration : IEntityTypeConfiguration<GarminLink>
{
    public void Configure(EntityTypeBuilder<GarminLink> builder)
    {
        builder.ToTable("garmin_links", "households", t => t.HasCheckConstraint("ck_garmin_link_state",
            "\"State\" IN ('never', 'ok', 'mfa_requested', 'code_refused', 'code_expired', 'credentials_refused', 'error')"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.State).HasMaxLength(30).IsRequired();
        builder.Property(l => l.Message).HasMaxLength(1000);
        builder.Property(l => l.LastError).HasMaxLength(1000);
        builder.Property(l => l.LastSuccessSummary).HasMaxLength(500);
        builder.Property(l => l.MfaCode).HasMaxLength(6);
        builder.HasIndex(l => l.UserId).IsUnique();
        builder.HasOne<Household>().WithMany().HasForeignKey(l => l.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
