using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Sports;

internal sealed class DailyChallengeConfiguration : IEntityTypeConfiguration<DailyChallenge>
{
    public void Configure(EntityTypeBuilder<DailyChallenge> builder)
    {
        builder.ToTable("daily_challenges", SportsSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_daily_challenge_unit", "\"Unit\" IN ('reps', 'minutes')");
            t.HasCheckConstraint("ck_daily_challenge_period", "\"StoppedOn\" IS NULL OR \"StoppedOn\" >= \"StartedOn\"");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasAlternateKey(c => new { c.Id, c.HouseholdId });
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Unit).HasMaxLength(10).IsRequired();
        builder.HasOne<Household>().WithMany().HasForeignKey(c => c.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(c => new { c.MemberProfileId, c.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.HouseholdId, c.MemberProfileId });
        builder.OwnsMany(c => c.Rules, rules =>
        {
            rules.ToTable("daily_challenge_rules", SportsSchema.Name, t =>
            {
                t.HasCheckConstraint("ck_challenge_rule_target", "\"DailyTarget\" > 0");
                t.HasCheckConstraint("ck_challenge_rule_jokers", "\"JokersPerWeek\" >= 0 AND \"JokersPerWeek\" <= 2");
            });
            rules.WithOwner().HasForeignKey("ChallengeId");
            rules.HasKey("ChallengeId", nameof(ChallengeRule.ValidFrom));
        });
        builder.Navigation(c => c.Rules).HasField("_rules").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ChallengeEntryConfiguration : IEntityTypeConfiguration<ChallengeEntry>
{
    public void Configure(EntityTypeBuilder<ChallengeEntry> builder)
    {
        builder.ToTable("challenge_entries", SportsSchema.Name, t =>
            t.HasCheckConstraint("ck_challenge_entry_quantity", "\"Quantity\" > 0 AND \"Quantity\" <= 10000"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasOne<DailyChallenge>().WithMany().HasForeignKey(e => new { e.ChallengeId, e.HouseholdId })
            .HasPrincipalKey(c => new { c.Id, c.HouseholdId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ChallengeId, e.Date });
    }
}
