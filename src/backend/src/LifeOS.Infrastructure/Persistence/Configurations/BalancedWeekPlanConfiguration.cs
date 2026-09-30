using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="BalancedWeekPlan"/> onto the historical <c>week_scenarios</c> table so the
/// switch from per-objective scenarios to a single balanced plan needs no schema migration.
/// </summary>
internal sealed class BalancedWeekPlanConfiguration : IEntityTypeConfiguration<BalancedWeekPlan>
{
    public void Configure(EntityTypeBuilder<BalancedWeekPlan> builder)
    {
        builder.ToTable("week_scenarios");

        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();

        builder.Property(plan => plan.WeekId).IsRequired();

        // Legacy column name kept: it used to hold the chosen ranking objective.
        builder.Property(plan => plan.Method)
            .HasColumnName("RankingObjective")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(plan => plan.Explanation)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(plan => plan.Applied)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(plan => plan.CreatedAt).IsRequired();
        builder.Property(plan => plan.UpdatedAt).IsRequired();

        builder.HasOne<Week>()
            .WithMany(week => week.BalancedPlans)
            .HasForeignKey(plan => plan.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(plan => plan.WeekId);
        builder.HasIndex(plan => new { plan.WeekId, plan.Method })
            .HasDatabaseName("IX_week_scenarios_WeekId_RankingObjective");
    }
}
