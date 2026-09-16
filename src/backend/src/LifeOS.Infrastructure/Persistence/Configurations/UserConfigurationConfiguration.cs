using LifeOS.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Persistence.Configurations;

internal sealed class UserConfigurationConfiguration : IEntityTypeConfiguration<UserConfiguration>
{
    public void Configure(EntityTypeBuilder<UserConfiguration> builder)
    {
        builder.ToTable("user_configurations");

        builder.HasKey(config => config.Id);
        builder.Property(config => config.Id).ValueGeneratedNever();

        builder.Property(config => config.HouseholdId).IsRequired();
        builder.Property(config => config.DailyBaseEnergyKcal)
            .IsRequired()
            .HasPrecision(10, 2);
        builder.Property(config => config.TargetNetDeficitKcal)
            .IsRequired()
            .HasPrecision(10, 2);
        builder.Property(config => config.TargetProteinG)
            .IsRequired()
            .HasPrecision(10, 2);
        builder.Property(config => config.TargetCarbsG)
            .IsRequired()
            .HasPrecision(10, 2);
        builder.Property(config => config.TargetFatsG)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(config => config.CreatedAt).IsRequired();
        builder.Property(config => config.UpdatedAt).IsRequired();

        // FK to Household
        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(config => config.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one config per household
        builder.HasIndex(config => config.HouseholdId).IsUnique();
    }
}
