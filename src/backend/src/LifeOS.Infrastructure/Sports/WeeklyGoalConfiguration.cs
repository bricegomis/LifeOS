using LifeOS.Domain.Households;
using LifeOS.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Sports;

internal sealed class WeeklyGoalConfiguration : IEntityTypeConfiguration<WeeklyGoal>
{
    public void Configure(EntityTypeBuilder<WeeklyGoal> builder)
    {
        builder.ToTable("weekly_goals", SportsSchema.Name, t =>
        {
            t.HasCheckConstraint("ck_weekly_goal_kind",
                "(\"Kind\" = 'template' AND \"TemplateId\" IS NOT NULL)" +
                $" OR (\"Kind\" = 'sport' AND \"Sport\" IN ({SportsSchema.SportCodes}))" +
                " OR \"Kind\" = 'sessions'" +
                " OR (\"Kind\" = 'volume' AND \"Unit\" IN ('kcal', 'km', 'hours'))");
            t.HasCheckConstraint("ck_weekly_goal_target", "\"Target\" > 0");
            t.HasCheckConstraint("ck_weekly_goal_period", "\"ValidUntil\" IS NULL OR \"ValidUntil\" > \"ValidFrom\"");
        });
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Kind).HasMaxLength(20).IsRequired();
        builder.Property(g => g.Sport).HasMaxLength(30);
        builder.Property(g => g.Unit).HasMaxLength(10);
        builder.Property(g => g.Target).HasPrecision(10, 2);
        builder.HasOne<Household>().WithMany().HasForeignKey(g => g.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(g => new { g.MemberProfileId, g.HouseholdId })
            .HasPrincipalKey(p => new { p.Id, p.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SportTemplate>().WithMany().HasForeignKey(g => new { g.TemplateId, g.HouseholdId })
            .HasPrincipalKey(t => new { t.Id, t.HouseholdId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => new { g.HouseholdId, g.MemberProfileId });
    }
}
