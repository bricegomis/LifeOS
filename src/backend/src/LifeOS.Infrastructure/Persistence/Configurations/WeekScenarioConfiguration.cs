using LifeOS.Domain.WeekPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class WeekScenarioConfiguration : IEntityTypeConfiguration<WeekScenario>
{
    public void Configure(EntityTypeBuilder<WeekScenario> builder)
    {
        builder.ToTable("week_scenarios");

        builder.HasKey(scenario => scenario.Id);
        builder.Property(scenario => scenario.Id).ValueGeneratedNever();

        builder.Property(scenario => scenario.WeekId).IsRequired();
        builder.Property(scenario => scenario.RankingObjective)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(scenario => scenario.Explanation)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(scenario => scenario.Applied)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(scenario => scenario.CreatedAt).IsRequired();
        builder.Property(scenario => scenario.UpdatedAt).IsRequired();

        // Relationship to Week
        builder.HasOne<Week>()
            .WithMany(week => week.Scenarios)
            .HasForeignKey(scenario => scenario.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(scenario => scenario.WeekId);
        builder.HasIndex(scenario => new { scenario.WeekId, scenario.RankingObjective });
    }
}
